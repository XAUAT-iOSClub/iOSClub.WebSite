import { apiRequest } from './ApiService';
import {url} from './Url';

/**
 * HTTP 性能指标 —— 对应后端 MonitoringController 的 http 字段。
 *
 * C30：后端改为固定统计窗口（windowSeconds，默认 3600），并用
 * averageRequestsPerSecond 取代了含义含混的 requestsPerSecond。
 */
export interface MonitoringHttpMetrics {
    totalRequests: number;
    /** 固定统计窗口（秒），3600 表示近 1 小时。 */
    windowSeconds: number;
    /** 窗口内平均每秒请求数（不是瞬时 QPS）。 */
    averageRequestsPerSecond: number;
    errorRate: number;
    averageResponseTime: number;
}

/** 系统指标 —— cpuSecondsTotal 是累计 CPU 秒数，不再是使用率。 */
export interface MonitoringSystemMetrics {
    cpuSecondsTotal: number;
}

export interface MonitoringPerformanceMetrics {
    http: MonitoringHttpMetrics;
    system?: MonitoringSystemMetrics;
    timestamp?: string;
}

/**
 * 监控数据服务
 */
export class MonitoringService {
    /**
     * 获取性能监控数据
     * @returns Promise<MonitoringPerformanceMetrics> 性能监控数据
     */
    static async getPerformanceMetrics(): Promise<MonitoringPerformanceMetrics> {
        return apiRequest<MonitoringPerformanceMetrics>({
            url: `${url}/Monitoring/performance`,
            requiresAuth: true
        });
    }

    /**
     * 获取HTTP请求统计数据
     * @returns Promise<any> HTTP请求统计数据
     */
    static async getHttpStats(): Promise<any> {
        return apiRequest({
            url: `${url}/Monitoring/http-stats`,
            requiresAuth: true
        });
    }

    /**
     * 获取数据访问统计
     * @param entityType 实体类型（可选）
     * @param top 返回前N条数据
     * @returns Promise<any> 数据访问统计数据
     */
    static async getDataAccessStats(entityType?: string, top: number = 10): Promise<any> {
        const params = new URLSearchParams();
        if (entityType) {
            params.append('entityType', entityType);
        }
        params.append('top', top.toString());

        return apiRequest({
            url: `${url}/Monitoring/data-access-stats?${params.toString()}`,
            requiresAuth: true
        });
    }

    /**
     * 获取数据变化统计
     * @param entityType 实体类型（可选）
     * @param top 返回前N条数据
     * @returns Promise<any> 数据变化统计数据
     */
    static async getDataChangeStats(entityType?: string, top: number = 10): Promise<any> {
        const params = new URLSearchParams();
        if (entityType) {
            params.append('entityType', entityType);
        }
        params.append('top', top.toString());

        return apiRequest({
            url: `${url}/Monitoring/data-change-stats?${params.toString()}`,
            requiresAuth: true
        });
    }

    /**
     * 重置数据统计
     * @param entityType 实体类型（可选）
     * @returns Promise<any> 重置结果
     */
    static async resetDataStats(entityType?: string): Promise<any> {
        const params = new URLSearchParams();
        if (entityType) {
            params.append('entityType', entityType);
        }

        return apiRequest({
            url: `${url}/Monitoring/reset-data-stats?${params.toString()}`,
            method: 'POST',
            requiresAuth: true
        });
    }
}
