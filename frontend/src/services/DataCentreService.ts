import {url} from './Url';
import {AuthService} from "./AuthService";
import {apiRequest} from './ApiService';

// 定义数据模型
export interface YearCount {
    year: string;
    value: number;
}

export interface AcademyCount {
    type: string;
    value: number;
}

export interface GradeCount {
    grade?: string;
    value?: number;
}

export interface GenderCount {
    type: string;
    value: number;
}

// 政治面貌数据可能需要的额外类型
export interface PoliticalCount {
    type: string;
    value: number;
}

// 备份导入的结果统计
export interface DataImportResult {
    added: number;
    updated: number;
    skipped: number;
}

// 数据中心服务
export class DataCentreService {
    // 获取历年人数数据
    static async getYearData(): Promise<YearCount[]> {
        return await apiRequest<YearCount[]>({
            url: `${url}/DataCentre/year`,
            method: 'GET'
        });
    }

    // 获取学院分布数据
    static async getCollegeData(): Promise<AcademyCount[]> {
        return await apiRequest<AcademyCount[]>({
            url: `${url}/DataCentre/college`,
            method: 'GET'
        });
    }

    // 获取年级分布数据
    static async getGradeData(): Promise<GradeCount[]> {
        return await apiRequest<GradeCount[]>({
            url: `${url}/DataCentre/grade`,
            method: 'GET'
        });
    }

    // 获取政治面貌分布数据
    static async getLandscapeData(): Promise<PoliticalCount[]> {
        return await apiRequest<PoliticalCount[]>({
            url: `${url}/DataCentre/landscape`,
            method: 'GET'
        });
    }

    // 获取性别分布数据
    static async getGenderData(): Promise<GenderCount[]> {
        return await apiRequest<GenderCount[]>({
            url: `${url}/DataCentre/gender`,
            method: 'GET'
        });
    }

    static async getCentreData(): Promise<any> {
        return await apiRequest<any>({
            url: `${url}/DataCentre`,
            method: 'GET'
        });
    }

    static async updateDataFromJson(file: File): Promise<DataImportResult> {
        const formData = new FormData();
        formData.append('file', file);

        // 通过统一请求层解析 ApiResponse；HTTP 200 的业务失败也会抛出错误。
        return await apiRequest<DataImportResult>({
            url: `${url}/DataCentre/update-from-json`,
            method: 'POST',
            body: formData
        });
    }

    static async exportJson(): Promise<Blob> {
        // 对于文件下载，我们直接使用fetch而不是apiRequest，因为apiRequest只处理JSON响应
        const token = AuthService.getToken();
        if (!token) {
            throw new Error('未登录');
        }

        const response = await fetch(`${url}/DataCentre/export-json`, {
            method: 'GET',
            headers: {
                'Authorization': `Bearer ${token}`,
            },
        });

        if (!response.ok) {
            // 尝试解析错误响应
            let errorMessage = `HTTP error! status: ${response.status}`;
            try {
                const errorData = await response.json();
                // 后端统一信封用的是 camelCase 的 message 字段
                if (errorData.message) {
                    errorMessage = errorData.message;
                }
            } catch {
            }
            throw new Error(errorMessage);
        }

        return await response.blob();
    }
}
