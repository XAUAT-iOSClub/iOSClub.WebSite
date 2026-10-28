import {url} from './Url';
import type {
    ClientApplication,
    ClientAppResultModel,
    CreateClientAppModel,
    UpdateClientAppModel,
    RegenerateSecretResult,
    ClientSecretResult
} from '../models';
import {apiRequest} from './ApiService';

/**
 * 客户端应用服务类 - 处理客户端应用管理相关的API调用
 */
export class ClientAppService {
    /**
     * 获取所有客户端应用
     * @returns Promise<ClientApplication[]> 客户端应用列表
     */
    static async getAllClientApplications(): Promise<ClientApplication[]> {
        return await apiRequest<ClientApplication[]>({
            url: `${url}/ClientApp`,
            method: 'GET'
        });
    }

    /**
     * 根据客户端ID获取应用
     * @param clientId 客户端ID
     * @returns Promise<ClientApplication> 客户端应用
     */
    static async getClientApplication(clientId: string): Promise<ClientApplication> {
        return await apiRequest<ClientApplication>({
            url: `${url}/ClientApp/${clientId}`,
            method: 'GET'
        });
    }

    /**
     * 创建新的客户端应用
     * @param clientAppModel 客户端应用信息
     * @returns Promise<ClientAppResultModel> 创建的客户端应用（包含密钥）
     */
    static async createClientApplication(clientAppModel: CreateClientAppModel): Promise<ClientAppResultModel> {
        return await apiRequest<ClientAppResultModel>({
            url: `${url}/ClientApp`,
            method: 'POST',
            body: JSON.stringify(clientAppModel)
        });
    }

    /**
     * 更新客户端应用
     * @param clientId 客户端ID
     * @param clientAppModel 客户端应用信息
     * @returns Promise<void>
     */
    static async updateClientApplication(clientId: string, clientAppModel: UpdateClientAppModel): Promise<void> {
        await apiRequest<void>({
            url: `${url}/ClientApp/${clientId}`,
            method: 'PUT',
            body: JSON.stringify(clientAppModel)
        });
    }

    /**
     * 删除客户端应用
     * @param clientId 客户端ID
     * @returns Promise<void>
     */
    static async deleteClientApplication(clientId: string): Promise<void> {
        await apiRequest<void>({
            url: `${url}/ClientApp/${clientId}`,
            method: 'DELETE'
        });
    }

    /**
     * 查看客户端应用的可恢复密钥（仅 Founder / President）
     * @param clientId 客户端ID
     * @returns Promise<ClientSecretResult> 原始密钥
     */
    static async getClientSecret(clientId: string): Promise<ClientSecretResult> {
        return await apiRequest<ClientSecretResult>({
            url: `${url}/ClientApp/${clientId}/secret`,
            method: 'GET'
        });
    }

    /**
     * 重新生成客户端密钥
     * @param clientId 客户端ID
     * @returns Promise<RegenerateSecretResult> 新的客户端密钥
     */
    static async regenerateClientSecret(clientId: string): Promise<RegenerateSecretResult> {
        return await apiRequest<RegenerateSecretResult>({
            url: `${url}/ClientApp/${clientId}/regenerate-secret`,
            method: 'POST'
        });
    }
}