/**
 * 客户端应用模型
 */
export interface ClientApplication {
    /**
     * 客户端ID（主键）
     */
    clientId: string;

    /**
     * 注意：列表/详情接口不再返回 ClientSecret。
     * 原始密钥只在创建或重置时通过 ClientAppResultModel 返回一次；
     * 之后 Founder/President 可通过 "查看密钥" 接口读取。
     */

    /**
     * 是否保存了可恢复的加密密钥。旧应用为 false，需要先重置密钥。
     */
    hasRecoverableSecret: boolean;

    /**
     * 应用名称
     */
    applicationName: string;
    
    /**
     * 应用描述
     */
    description: string;
    
    /**
     * 应用主页URL
     */
    homepageUrl: string;
    
    /**
     * 回调URL白名单（后端 ClientAppVO.RedirectUris 为字符串数组）
     */
    redirectUris: string[];
    
    /**
     * 应用图标URL
     */
    logoUrl: string;
    
    /**
     * 是否启用
     */
    isActive: boolean;
    
    /**
     * 创建时间
     */
    createdAt: string;
    
    /**
     * 最后更新时间
     */
    updatedAt: string;

    /**
     * 是否支持PKCE
     */
    supportsPkce: boolean;

    /**
     * 是否需要邮箱验证
     */
    isNeedEMail: boolean;
}

/**
 * 创建客户端应用模型
 */
export interface CreateClientAppModel {
    /**
     * 应用名称
     */
    applicationName: string;
    
    /**
     * 应用描述
     */
    description: string;
    
    /**
     * 应用主页URL
     */
    homepageUrl: string;
    
    /**
     * 回调URL列表
     */
    redirectUris: string[];
    
    /**
     * 应用图标URL
     */
    logoUrl: string;
    
    /**
     * 是否支持PKCE
     */
    supportsPkce: boolean;

    /**
     * 是否需要邮箱验证
     */
    isNeedEMail: boolean;
}

/**
 * 更新客户端应用模型
 */
export interface UpdateClientAppModel {
    /**
     * 应用名称
     */
    applicationName: string;
    
    /**
     * 应用描述
     */
    description: string;
    
    /**
     * 应用主页URL
     */
    homepageUrl: string;
    
    /**
     * 回调URL列表
     */
    redirectUris: string[];
    
    /**
     * 应用图标URL
     */
    logoUrl: string;
    
    /**
     * 是否启用
     */
    isActive: boolean;
    
    /**
     * 是否支持PKCE
     */
    supportsPkce: boolean;

    /**
     * 是否需要邮箱验证
     */
    isNeedEMail: boolean;
}

/**
 * 客户端应用结果模型（包含密钥）
 */
export interface ClientAppResultModel {
    /**
     * 客户端ID
     */
    clientId: string;
    
    /**
     * 客户端密钥
     */
    clientSecret: string;
    
    /**
     * 应用名称
     */
    applicationName: string;
    
    /**
     * 应用描述
     */
    description: string;
    
    /**
     * 应用主页URL
     */
    homepageUrl: string;
    
    /**
     * 回调URL列表
     */
    redirectUris: string[];
    
    /**
     * 应用图标URL
     */
    logoUrl: string;
    
    /**
     * 是否启用
     */
    isActive: boolean;
    
    /**
     * 是否支持PKCE
     */
    supportsPkce: boolean;

    /**
     * 是否需要邮箱验证
     */
    isNeedEMail: boolean;
}

/**
 * 重新生成密钥结果 —— 对应后端 VOs/ClientAppResultVO.cs
 */
export interface RegenerateSecretResult {
    /**
     * 客户端ID
     */
    clientId: string;

    /**
     * 新的客户端密钥（后端字段名为 clientSecret，不是 newSecret）
     */
    clientSecret: string;
}

/**
 * 查看 OAuth 客户端密钥的结果
 */
export interface ClientSecretResult {
    clientId: string;
    clientSecret: string;
}