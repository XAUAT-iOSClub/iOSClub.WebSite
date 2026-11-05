import {url} from './Url';
import {apiRequest} from './ApiService';

/** 操作日志列表项，与后端 OperationLogVO 对齐。 */
export interface OperationLogItem {
    id: number;
    operatorId?: string | null;
    operatorName?: string | null;
    operatorRole?: string | null;
    module: string;
    action: string;
    controllerName: string;
    actionName: string;
    method: string;
    path?: string | null;
    summary?: string | null;
    target?: string | null;
    changeContent?: string | null;
    message?: string | null;
    errorMessage?: string | null;
    success: boolean;
    httpStatus: number;
    durationMs: number;
    traceId?: string | null;
    ipAddress?: string | null;
    userAgent?: string | null;
    createdAt: string;
}

/** 操作日志分页结果。 */
export interface OperationLogPage {
    items: OperationLogItem[];
    totalCount: number;
    pageIndex: number;
    pageSize: number;
    totalPages: number;
}

/** 操作人筛选选项。 */
export interface OperationLogOperator {
    userId: string;
    name?: string | null;
}

/** 筛选下拉选项。 */
export interface OperationLogOptions {
    modules: string[];
    operators: OperationLogOperator[];
}

/** 操作日志查询条件。 */
export interface OperationLogQuery {
    pageIndex?: number;
    pageSize?: number;
    module?: string | null;
    operatorId?: string | null;
    keyword?: string | null;
    success?: boolean | null;
    startTime?: string | null;
    endTime?: string | null;
}

/**
 * 全局操作日志服务。后端仅允许 Founder / President 读取。
 */
export class OperationLogService {
    static async getLogs(query: OperationLogQuery = {}): Promise<OperationLogPage> {
        const params = new URLSearchParams();
        if (query.pageIndex) params.set('pageIndex', String(query.pageIndex));
        if (query.pageSize) params.set('pageSize', String(query.pageSize));
        if (query.module) params.set('module', query.module);
        if (query.operatorId) params.set('operatorId', query.operatorId);
        if (query.keyword) params.set('keyword', query.keyword);
        if (query.success !== null && query.success !== undefined) params.set('success', String(query.success));
        if (query.startTime) params.set('startTime', query.startTime);
        if (query.endTime) params.set('endTime', query.endTime);

        const suffix = params.toString();
        return apiRequest<OperationLogPage>({
            url: `${url}/OperationLogs${suffix ? `?${suffix}` : ''}`,
            method: 'GET'
        });
    }

    static async getOptions(): Promise<OperationLogOptions> {
        return apiRequest<OperationLogOptions>({
            url: `${url}/OperationLogs/options`,
            method: 'GET'
        });
    }
}
