import { url } from './Url';
import { apiRequest } from './ApiService';
import type {
  ActivityModel,
  ActivityCreateDto,
  ActivityStatusValue,
  ParticipantModel,
  ParticipantCreateDto,
  ParticipantPage,
  ImportResult,
  ActivityOperationLog
} from '../models';

function token() {
  return localStorage.getItem('accessToken') || '';
}

async function downloadBlob(path: string, fallbackName: string, init: RequestInit = {}) {
  const res = await fetch(`${url}${path}`, {
    ...init,
    headers: { Authorization: `Bearer ${token()}`, ...(init.headers || {}) }
  });
  if (!res.ok) throw new Error('下载失败');
  const blob = await res.blob();
  let filename = fallbackName;
  const disp = res.headers.get('Content-Disposition') || '';
  const star = disp.match(/filename\*=UTF-8''([^;]+)/i);
  const plain = disp.match(/filename="?([^";]+)"?/i);
  if (star) { try { filename = decodeURIComponent(star[1].trim()); } catch { filename = star[1].trim(); } }
  else if (plain) { filename = plain[1].trim(); }
  const a = document.createElement('a');
  a.href = URL.createObjectURL(blob);
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(a.href);
}

export class ActivityService {
  static list(status?: string, keyword?: string): Promise<ActivityModel[]> {
    const p = new URLSearchParams();
    if (status) p.set('status', status);
    if (keyword) p.set('keyword', keyword);
    return apiRequest<ActivityModel[]>({ url: `${url}/Activity?${p.toString()}`, method: 'GET' });
  }

  static get(id: string): Promise<ActivityModel> {
    return apiRequest<ActivityModel>({ url: `${url}/Activity/${id}`, method: 'GET' });
  }

  static create(dto: ActivityCreateDto): Promise<ActivityModel> {
    return apiRequest<ActivityModel>({ url: `${url}/Activity`, method: 'POST', body: JSON.stringify(dto) });
  }

  static update(id: string, dto: ActivityCreateDto): Promise<void> {
    return apiRequest<void>({ url: `${url}/Activity/${id}`, method: 'PUT', body: JSON.stringify(dto) });
  }

  static remove(id: string): Promise<void> {
    return apiRequest<void>({ url: `${url}/Activity/${id}`, method: 'DELETE' });
  }

  /** 手动变更状态：任意状态 → 任意状态 */
  static changeStatus(id: string, status: ActivityStatusValue): Promise<void> {
    return apiRequest<void>({ url: `${url}/Activity/${id}/status`, method: 'POST', body: JSON.stringify({ status }) });
  }

  static setSelfRegistration(id: string, enabled: boolean): Promise<void> {
    return apiRequest<void>({ url: `${url}/Activity/${id}/self-registration`, method: 'POST', body: JSON.stringify({ enabled }) });
  }

  static logs(id: string): Promise<ActivityOperationLog[]> {
    return apiRequest<ActivityOperationLog[]>({ url: `${url}/Activity/${id}/logs`, method: 'GET' });
  }

  // ===== 参与者 =====
  static participants(activityId: string, params: { search?: string; academy?: string; page?: number; pageSize?: number } = {}): Promise<ParticipantPage> {
    const p = new URLSearchParams();
    if (params.search) p.set('search', params.search);
    if (params.academy) p.set('academy', params.academy);
    p.set('page', String(params.page ?? 1));
    p.set('pageSize', String(params.pageSize ?? 20));
    return apiRequest<ParticipantPage>({ url: `${url}/Activity/${activityId}/participants?${p.toString()}`, method: 'GET' });
  }

  static addParticipant(activityId: string, dto: ParticipantCreateDto): Promise<ParticipantModel> {
    return apiRequest<ParticipantModel>({ url: `${url}/Activity/${activityId}/participants`, method: 'POST', body: JSON.stringify(dto) });
  }

  static updateParticipant(participantId: string, dto: ParticipantCreateDto): Promise<void> {
    return apiRequest<void>({ url: `${url}/Activity/participants/${participantId}`, method: 'PUT', body: JSON.stringify(dto) });
  }

  static removeParticipant(participantId: string): Promise<void> {
    return apiRequest<void>({ url: `${url}/Activity/participants/${participantId}`, method: 'DELETE' });
  }

  static bulkDeleteParticipants(activityId: string, ids: string[]): Promise<{ deleted: number }> {
    return apiRequest<{ deleted: number }>({ url: `${url}/Activity/${activityId}/participants/bulk-delete`, method: 'POST', body: JSON.stringify({ ids }) });
  }

  static register(activityId: string, dto: ParticipantCreateDto): Promise<ParticipantModel> {
    return apiRequest<ParticipantModel>({ url: `${url}/Activity/${activityId}/register`, method: 'POST', body: JSON.stringify(dto), requiresAuth: false });
  }

  // ===== Excel =====
  /** 导出参与者名单；backup=true 时文件名带“_备份”后缀。 */
  static exportExcel(activityId: string, backup = false): Promise<void> {
    return downloadBlob(`/Activity/${activityId}/export?backup=${backup}`, backup ? '活动参与者名单_备份.xlsx' : '活动参与者名单.xlsx');
  }

  static downloadTemplate(): Promise<void> {
    return downloadBlob('/Activity/import-template', '活动参与者导入模板.xlsx');
  }

  static async importExcel(activityId: string, file: File, preview: boolean): Promise<ImportResult> {
    const fd = new FormData();
    fd.append('file', file);
    const res = await fetch(`${url}/Activity/${activityId}/import?preview=${preview}`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${token()}` },
      body: fd
    });
    const payload = await res.json();
    if (!payload || payload.errorCode !== 0) throw new Error(payload?.message || '导入失败');
    return payload.data as ImportResult;
  }

  static downloadErrorReport(activityId: string, file: File): Promise<void> {
    const fd = new FormData();
    fd.append('file', file);
    return downloadBlob(`/Activity/${activityId}/import-error-report`, '导入错误报告.xlsx', { method: 'POST', body: fd });
  }
}
