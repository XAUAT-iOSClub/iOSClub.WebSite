import {url} from './Url';
import {apiRequest} from './ApiService';
import type {BackupStatus, BackupDownloadInfo} from '../models';

/**
 * 数据库备份只读接口。
 * 只查询最近一次成功备份的元数据并获取短时下载链接，不触发新的 pg_dump。
 */
export class AdminBackupService {
    /**
     * 查询最近一次成功备份的元数据。
     */
    static async getStatus(): Promise<BackupStatus> {
        return await apiRequest<BackupStatus>({
            url: `${url}/AdminBackups/status`,
            method: 'GET'
        });
    }

    /**
     * 获取最近一次成功备份的短时下载链接。
     */
    static async getLatestDownloadUrl(): Promise<BackupDownloadInfo> {
        return await apiRequest<BackupDownloadInfo>({
            url: `${url}/AdminBackups/latest/download-url`,
            method: 'GET'
        });
    }
}
