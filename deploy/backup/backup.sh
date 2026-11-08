#!/usr/bin/env bash
#
# iosclub-backup.sh
# 独立数据库备份执行器：pg_dump -> 校验 -> 上传 Cloudflare R2 -> 更新 latest.json。
# 不依赖 WebAPI 进程，也不由网页触发。日常调度交给 systemd timer。
#
set -Eeuo pipefail
umask 077

log() {
    printf '%s %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$*" >&2
}

# 手动运行时从配置文件加载；systemd 部署时通过 EnvironmentFile 注入，同样兼容。
ENV_FILE="${BACKUP_ENV_FILE:-/etc/iosclub-backup/backup.env}"
if [[ -f "$ENV_FILE" ]]; then
    set -a
    # shellcheck disable=SC1090
    source "$ENV_FILE"
    set +a
fi

require() {
    local name="$1"
    if [[ -z "${!name:-}" ]]; then
        log "缺少必需环境变量: $name"
        exit 2
    fi
}

for name in PGHOST PGDATABASE PGUSER PGPASSFILE BACKUP_DIR R2_ENDPOINT_URL R2_BUCKET; do
    require "$name"
done

: "${PGPORT:=5432}"
: "${R2_REGION:=auto}"
: "${R2_PREFIX:=backups/postgresql/}"
: "${R2_LATEST_MANIFEST_KEY:=metadata/latest.json}"
: "${BACKUP_LOCAL_RETENTION_DAYS:=3}"
: "${BACKUP_MIN_FREE_SPACE_MB:=2048}"
: "${BACKUP_INTERVAL_DAYS:=30}"
: "${BACKUP_FORCE:=0}"
: "${PGCONNECT_TIMEOUT:=10}"

export PGCONNECT_TIMEOUT
export AWS_EC2_METADATA_DISABLED=true

# 统一前缀格式，供后端校验 objectKey 时复用。
R2_PREFIX="${R2_PREFIX#/}"
[[ "$R2_PREFIX" == */ ]] || R2_PREFIX="${R2_PREFIX}/"
R2_LATEST_MANIFEST_KEY="${R2_LATEST_MANIFEST_KEY#/}"

TMP_DIR="$BACKUP_DIR/tmp"
DONE_DIR="$BACKUP_DIR/completed"
FAILED_DIR="$BACKUP_DIR/failed"
LOCK_FILE="${BACKUP_LOCK_FILE:-$BACKUP_DIR/.backup.lock}"
MARKER_FILE="${BACKUP_MARKER_FILE:-$BACKUP_DIR/.last-success}"

mkdir -p "$TMP_DIR" "$DONE_DIR" "$FAILED_DIR"

# 并发锁：定时任务与手动执行不能同时运行。
exec 9>"$LOCK_FILE"
if ! flock -n 9; then
    log "已有备份任务在运行，退出"
    exit 75
fi

# 磁盘空间检查。
available_mb="$(df -Pm "$BACKUP_DIR" | awk 'NR==2 {print $4}')"
if [[ -z "$available_mb" || "$available_mb" -lt "$BACKUP_MIN_FREE_SPACE_MB" ]]; then
    log "磁盘剩余空间不足: ${available_mb:-unknown}MB < ${BACKUP_MIN_FREE_SPACE_MB}MB"
    exit 3
fi

# 备份周期控制：定时器可以每日触发，但只有距离上次成功超过 BACKUP_INTERVAL_DAYS
# 才真正执行 pg_dump。BACKUP_FORCE=1 可绕过（手动/测试用）。
if [[ "$BACKUP_FORCE" != "1" && "$BACKUP_INTERVAL_DAYS" -gt 0 && -f "$MARKER_FILE" ]]; then
    last_epoch="$(cat "$MARKER_FILE" 2>/dev/null || echo 0)"
    now_epoch="$(date -u +%s)"
    if [[ "$last_epoch" =~ ^[0-9]+$ ]]; then
        elapsed_days=$(( (now_epoch - last_epoch) / 86400 ))
        if (( elapsed_days < BACKUP_INTERVAL_DAYS )); then
            log "距离上次成功备份 ${elapsed_days} 天，未到 ${BACKUP_INTERVAL_DAYS} 天周期，跳过"
            exit 0
        fi
    fi
fi

timestamp="$(date -u +%Y-%m-%dT%H%M%SZ)"
date_path="$(date -u +%Y/%m/%d)"
base_name="iosclub_${timestamp}.dump"
tmp_file="$(mktemp "$TMP_DIR/${base_name}.XXXXXX")"
dump_validated=0

cleanup() {
    local status=$?
    trap - EXIT

    if [[ $status -ne 0 && -f "$tmp_file" ]]; then
        if [[ $dump_validated -eq 1 ]]; then
            local failed_name="${base_name}.failed.dump"
            if mv "$tmp_file" "$FAILED_DIR/$failed_name" 2>/dev/null; then
                log "备份失败，已通过校验的转储保留到 $FAILED_DIR/$failed_name"
            else
                rm -f "$tmp_file"
            fi
        else
            rm -f "$tmp_file"
        fi
    fi

    exit $status
}
trap cleanup EXIT

log "开始 pg_dump: ${PGUSER}@${PGHOST}:${PGPORT}/${PGDATABASE}"
pg_dump --format=custom --compress=9 --no-owner --no-acl --file="$tmp_file"

if ! pg_restore --list "$tmp_file" >/dev/null; then
    log "pg_restore --list 校验失败"
    exit 4
fi
dump_validated=1

size_bytes="$(stat -c %s "$tmp_file")"
sha256="$(sha256sum "$tmp_file" | awk '{print $1}')"
log "备份生成完成: size=${size_bytes} sha256=${sha256}"

object_key="${R2_PREFIX}${date_path}/${base_name}"
aws_args=(--endpoint-url "$R2_ENDPOINT_URL" --region "$R2_REGION")

log "上传到 R2: s3://${R2_BUCKET}/${object_key}"
aws "${aws_args[@]}" s3 cp "$tmp_file" "s3://${R2_BUCKET}/${object_key}" \
    --content-type application/octet-stream \
    --content-disposition "attachment; filename=\"${base_name}\""

remote_size="$(aws "${aws_args[@]}" s3api head-object \
    --bucket "$R2_BUCKET" --key "$object_key" --query ContentLength --output text)"
if [[ "$remote_size" != "$size_bytes" ]]; then
    log "R2 对象大小校验失败: remote=${remote_size} local=${size_bytes}"
    exit 5
fi
log "R2 对象上传并校验通过"

manifest_file="$(mktemp "$TMP_DIR/latest.XXXXXX.json")"
cat > "$manifest_file" <<JSON
{
  "status": "success",
  "createdAt": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "objectKey": "${object_key}",
  "sizeBytes": ${size_bytes},
  "sha256": "${sha256}",
  "format": "pg_dump-custom",
  "fileName": "${base_name}"
}
JSON

# 只有上传与大小校验都成功后才更新 latest.json。
aws "${aws_args[@]}" s3 cp "$manifest_file" "s3://${R2_BUCKET}/${R2_LATEST_MANIFEST_KEY}" \
    --content-type application/json
rm -f "$manifest_file"
log "latest.json 已更新: ${R2_LATEST_MANIFEST_KEY}"

mv "$tmp_file" "$DONE_DIR/$base_name"
log "本地备份已保存: $DONE_DIR/$base_name"

# 记录本次成功时间，供 30 天周期判断使用。
date -u +%s > "$MARKER_FILE"

# 本地只保留近期副本；R2 的 90 天保留由 Bucket 生命周期规则负责。
while IFS= read -r old_file; do
    [[ -n "$old_file" ]] || continue
    rm -f -- "$old_file"
    log "已删除过期本地备份: $old_file"
done < <(find "$DONE_DIR" -maxdepth 1 -type f -name 'iosclub_*.dump' \
    -mtime +"$BACKUP_LOCAL_RETENTION_DAYS" -print)

log "备份成功完成: ${base_name}"
