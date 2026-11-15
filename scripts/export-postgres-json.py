#!/usr/bin/env python3
"""Export all non-system PostgreSQL tables using one read-only snapshot."""

import argparse
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
from datetime import datetime

ROOT = Path(__file__).resolve().parents[1]
TABLES = """
FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind IN ('r', 'p') AND NOT c.relispartition
  AND n.nspname NOT IN ('pg_catalog', 'information_schema')
  AND n.nspname !~ '^pg_(toast|temp)'
"""
QUERY = """
BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY;
SET LOCAL row_security = off;
SET LOCAL timezone = 'UTC';
SELECT json_build_object(
  'formatVersion', 1, 'database', current_database(),
  'exportedAt', CURRENT_TIMESTAMP,
  'tableCount', (SELECT count(*)
""" + TABLES + """));
SELECT string_agg(format(
  'SELECT json_build_object(''schema'', %L, ''name'', %L,
    ''rowCount'', count(*)) FROM %I.%I;
    SELECT row_to_json(t) FROM %I.%I t;',
    n.nspname, c.relname, n.nspname, c.relname, n.nspname, c.relname),
    E'\n' ORDER BY n.nspname, c.relname)
""" + TABLES + """
\\gexec
COMMIT;
"""


def connection_environment(connection):
    pattern = re.compile(
        r'''\s*([^=;]+?)\s*=\s*(?:"((?:""|[^"])*)"|'((?:''|[^'])*)'|([^;]*))\s*(?:;|$)'''
    )
    settings = {}
    position = 0
    while position < len(connection.rstrip()):
        match = pattern.match(connection, position)
        if not match:
            raise ValueError("SQL 连接字符串格式无效")
        key, double, single, plain = match.groups()
        value = (double.replace('""', '"') if double is not None else
                 single.replace("''", "'") if single is not None else plain.strip())
        settings[key.lower()] = value
        position = match.end()
    environment = os.environ.copy()
    aliases = {
        'host': 'PGHOST', 'server': 'PGHOST', 'port': 'PGPORT',
        'database': 'PGDATABASE', 'username': 'PGUSER', 'user id': 'PGUSER',
        'password': 'PGPASSWORD', 'timeout': 'PGCONNECT_TIMEOUT',
    }
    for key, target in aliases.items():
        if key in settings:
            environment[target] = settings[key]
    ssl_mode = settings.get('ssl mode', '').lower().replace(' ', '')
    if ssl_mode:
        environment['PGSSLMODE'] = {'verifyca': 'verify-ca', 'verifyfull': 'verify-full'}.get(
            ssl_mode, ssl_mode)
    environment.setdefault('PGCONNECT_TIMEOUT', '15')
    environment['PGCLIENTENCODING'] = 'UTF8'
    return environment


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--env-file', type=Path,
                        default=ROOT / 'rearend/iOSClub.WebAPI/iOSClub.WebAPI/.env')
    parser.add_argument('--output', type=Path, default=ROOT / 'backups' /
                        f'database-{datetime.now():%Y%m%d-%H%M%S}.json')
    args = parser.parse_args()
    if not shutil.which('psql'):
        parser.error('需要安装 PostgreSQL 客户端 psql')
    connection = os.environ.get('SQL')
    if not connection:
        try:
            from dotenv import dotenv_values
        except ImportError:
            parser.error('请先运行 python3 -m pip install python-dotenv')
        if not args.env_file.is_file():
            parser.error('.env 文件不存在，请使用 --env-file 指定')
        connection = dotenv_values(args.env_file).get('SQL')
    if not connection:
        parser.error('SQL 未配置；此脚本用于 PostgreSQL')
    environment = connection_environment(connection)
    output = args.output.resolve()
    if output.exists():
        parser.error('目标文件已存在，请使用 --output 指定其他文件')
    output.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary = tempfile.mkstemp(prefix='.export-', dir=output.parent)
    try:
        with os.fdopen(descriptor, 'w', encoding='utf-8') as destination, \
                tempfile.TemporaryFile(mode='w+', encoding='utf-8') as errors, \
                subprocess.Popen(
                    ['psql', '-X', '-w', '-q', '-A', '-t', '-v', 'ON_ERROR_STOP=1', '-f', '-'],
                    env=environment, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                    stderr=errors, text=True, encoding='utf-8') as process:
            process.stdin.write(QUERY)
            process.stdin.close()
            first_line = process.stdout.readline()
            if not first_line:
                process.wait()
                errors.seek(0)
                message = errors.read().replace(environment.get('PGPASSWORD') or '\0', '[redacted]')
                raise RuntimeError(message.strip() or '数据库没有返回导出数据')
            metadata = json.loads(first_line)
            destination.write(json.dumps(metadata, ensure_ascii=False)[:-1] + ',\n  "tables": [\n')
            table_count = total_rows = 0
            for _ in range(metadata['tableCount']):
                table = json.loads(process.stdout.readline())
                if table_count:
                    destination.write(',\n')
                destination.write(json.dumps(table, ensure_ascii=False)[:-1] + ', "rows": [\n')
                for row_number in range(table['rowCount']):
                    line = process.stdout.readline()
                    json.loads(line)
                    if row_number:
                        destination.write(',\n')
                    destination.write(line.rstrip('\n'))
                destination.write('\n]}')
                table_count += 1
                total_rows += table['rowCount']
                print(f"{table['schema']}.{table['name']}: {table['rowCount']} rows", flush=True)
            if process.stdout.read().strip():
                raise ValueError('数据库返回了额外的导出记录')
            if process.wait():
                errors.seek(0)
                message = errors.read().replace(environment.get('PGPASSWORD') or '\0', '[redacted]')
                raise RuntimeError(message.strip())
            if table_count != metadata['tableCount']:
                raise ValueError('导出表数量校验失败')
            destination.write('\n  ]\n}\n')
        os.link(temporary, output)
        print(f'已导出 {table_count} 张表，{total_rows} 条记录：{output}')
    finally:
        Path(temporary).unlink(missing_ok=True)


if __name__ == '__main__':
    try:
        main()
    except (OSError, ValueError, RuntimeError) as error:
        raise SystemExit(f'导出失败：{error}')
