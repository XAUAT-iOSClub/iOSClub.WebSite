#!/usr/bin/env bash
set -euo pipefail

CONTAINER_NAME="iosclub-webapi"
SERVICE_NAME="app"
DEFAULT_IMAGE="ghcr.io/${GITHUB_REPOSITORY:-xauat-iosclub/iosclub.website}-backend:latest"
IMAGE="${IMAGE:-${1:-$DEFAULT_IMAGE}}"
REGISTRY_HOST="${IMAGE%%/*}"
APP_PORT="${APP_PORT:-8080}"
READY_TIMEOUT="${READY_TIMEOUT:-60}"
COMPOSE_PROJECT_NAME="${COMPOSE_PROJECT_NAME:-iosclub-webapi}"
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

export IMAGE APP_PORT COMPOSE_PROJECT_NAME
command -v docker >/dev/null 2>&1 || { echo "错误：未找到 docker。" >&2; exit 1; }
docker compose version >/dev/null 2>&1 || { echo "错误：需要 docker compose v2。" >&2; exit 1; }
[[ "$APP_PORT" =~ ^[0-9]+$ ]] && (( APP_PORT >= 1 && APP_PORT <= 65535 )) || {
  echo "错误：APP_PORT 必须是 1 到 65535 之间的端口号。" >&2; exit 1;
}
[[ -f .env ]] || { echo "错误：请先在 deploy 目录创建 .env。" >&2; exit 1; }

if docker container inspect "$CONTAINER_NAME" >/dev/null 2>&1; then
  owner="$(docker container inspect -f '{{index .Config.Labels "com.docker.compose.project"}}' "$CONTAINER_NAME" 2>/dev/null || true)"
  [[ "$owner" == "<no value>" ]] && owner=""
  if [[ -z "$owner" ]]; then
    if [[ "${TAKE_OVER:-0}" == "1" ]]; then
      docker rm -f "$CONTAINER_NAME" >/dev/null
    else
      echo "错误：$CONTAINER_NAME 是非 Compose 容器。确认可删除后运行 TAKE_OVER=1 $0。" >&2
      exit 1
    fi
  elif [[ "$owner" != "$COMPOSE_PROJECT_NAME" ]]; then
    echo "错误：$CONTAINER_NAME 属于 Compose project '$owner'，请设置 COMPOSE_PROJECT_NAME=$owner 或先迁移容器。" >&2
    exit 1
  fi
fi

if [[ -n "${PULL_TOKEN:-}" ]]; then
  : "${PULL_USERNAME:?设置 PULL_TOKEN 时还必须设置 PULL_USERNAME}"
  trap 'docker logout "$REGISTRY_HOST" >/dev/null 2>&1 || true' EXIT
  printf '%s' "$PULL_TOKEN" | docker login "$REGISTRY_HOST" --username "$PULL_USERNAME" --password-stdin
fi

previous_image="$(docker container inspect -f '{{.Config.Image}}' "$CONTAINER_NAME" 2>/dev/null || true)"
echo "==> 拉取镜像 $IMAGE"
docker compose -f docker-compose.production.yml pull "$SERVICE_NAME"
echo "==> 启动服务"
docker compose -f docker-compose.production.yml up -d --no-build --remove-orphans "$SERVICE_NAME"

echo "==> 等待应用启动（最多 ${READY_TIMEOUT}s）"
deadline=$((SECONDS + READY_TIMEOUT))
ready=0
while (( SECONDS < deadline )); do
  running="$(docker container inspect -f '{{.State.Running}}' "$CONTAINER_NAME" 2>/dev/null || echo false)"
  if [[ "$running" != "true" ]]; then
    echo "错误：容器已退出。最近日志：" >&2
    docker logs --tail 60 "$CONTAINER_NAME" >&2 || true
    exit 1
  fi
  logs="$(docker logs "$CONTAINER_NAME" 2>&1 || true)"
  if [[ "$logs" == *"Now listening on"* || "$logs" == *"Application started"* ]]; then ready=1; break; fi
  sleep 1
done
if (( ! ready )); then
  echo "错误：${READY_TIMEOUT}s 内没有等到服务启动。" >&2
  docker logs --tail 60 "$CONTAINER_NAME" >&2 || true
  exit 1
fi

docker image prune --force >/dev/null
echo "==> 部署完成：$IMAGE（宿主机端口 $APP_PORT）"
echo "    日志：docker logs -f $CONTAINER_NAME"
if [[ -n "$previous_image" && "$previous_image" != "$IMAGE" ]]; then
  echo "    回滚：$0 $previous_image"
fi
