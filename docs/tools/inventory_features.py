from pathlib import Path
import re


root = Path(__file__).resolve().parents[2]
target = root / "docs/功能入口索引.md"
rows = []
router = root / "frontend/src/router.ts"
for number, line in enumerate(router.read_text().splitlines(), 1):
    for match in re.finditer(r"\bpath:\s*(['\"])(.*?)\1", line):
        rows.append(("前端路径声明", router.relative_to(root), number, match[2]))

controllers = root / "rearend/iOSClub.WebAPI/iOSClub.WebAPI/Controllers"
for source in sorted(controllers.glob("*.cs")):
    for number, line in enumerate(source.read_text().splitlines(), 1):
        for match in re.finditer(r"\[(?:Http(?:Get|Post|Put|Delete|Patch)|Route)\b[^\]]*\]", line):
            rows.append(("后端路由特性", source.relative_to(root), number, match[0]))

content = [
    "# 功能入口索引",
    "",
    "由 `python3 docs/tools/inventory_features.py` 从当前源码生成。",
    "",
    "本表记录前端 path 声明和后端 Route、Http 特性，不解析嵌套路由、权限继承或运行时注册。",
    "前端子路径需要与父路径组合。后端特性需要与控制器前缀组合。入口存在不代表功能可用。",
    "业务能力和冲突结论见 [PRD](PRD-iOSClub-功能现状与冲突梳理.md)。",
    "",
    "| 类型 | 源码 | 声明 |",
    "| --- | --- | --- |",
]
for kind, source, number, declaration in rows:
    safe = declaration.replace("|", "&#124;")
    content.append(f"| {kind} | [{source.name}:{number}](../{source}#L{number}) | `{safe}` |")
target.write_text("\n".join(content) + "\n")
print(f"已生成 {target.relative_to(root)}，共 {len(rows)} 条源码声明。")
