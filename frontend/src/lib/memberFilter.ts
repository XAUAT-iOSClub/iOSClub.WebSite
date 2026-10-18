/**
 * 在校成员判定。
 *
 * 约定：学号前两位表示入学年份（例如 23 表示 2023 年入学）。
 * 入学年份 <= 本年 - 4 视为已毕业，不再属于在校成员。
 * 例：2026 年时 cutoff = 22，因此 22 级及更早的学号不导出。
 */
export function isInSchoolMember(userId: string, now: Date = new Date()): boolean {
  const prefix = Number.parseInt((userId || '').slice(0, 2), 10)
  // 学号不是以两位数年份开头时无法判断，默认保留（不误删）
  if (Number.isNaN(prefix)) return true
  const year = now.getFullYear()
  const cutoff = (((year - 4) % 100) + 100) % 100
  return prefix > cutoff
}

export function filterInSchool<T extends {userId: string}>(list: T[], now: Date = new Date()): T[] {
  return list.filter(member => isInSchoolMember(member.userId, now))
}
