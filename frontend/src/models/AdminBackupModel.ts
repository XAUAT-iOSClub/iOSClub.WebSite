/**
 * 最近一次数据库备份的元数据（不含凭据）。
 */
export interface BackupStatus {
    fileName: string;
    sizeBytes: number;
    createdAt: string | null;
    status: string;
    sha256?: string | null;
    format?: string | null;
}

/**
 * 最新备份的短时下载信息。
 */
export interface BackupDownloadInfo {
    fileName: string;
    sizeBytes: number;
    createdAt: string | null;
    downloadUrl: string;
}
