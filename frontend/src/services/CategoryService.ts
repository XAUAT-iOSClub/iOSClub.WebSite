import {url} from './Url';
import {ArticleModel, CategoryModel} from '../models';
import {apiRequest} from './ApiService';

/**
 * 分类服务类 - 处理文章分类相关的API调用
 */
export class CategoryService {
    /**
     * 获取所有分类
     * @returns Promise<CategoryModel[]> 分类列表
     */
    static async getAllCategories(): Promise<CategoryModel[]> {
        return await apiRequest<CategoryModel[]>({
            url: `${url}/Category/all`,
            method: 'GET'
        });
    }

    /**
     * 根据名称获取分类
     * @param name 分类名称
     * @returns Promise<CategoryModel> 分类信息
     */
    static async getCategory(name: string): Promise<CategoryModel> {
        return await apiRequest<CategoryModel>({
            url: `${url}/Category/${name}`,
            method: 'GET'
        });
    }

    /**
     * 根据Id获取分类
     * @param id 分类Id
     * @returns Promise<CategoryModel> 分类信息
     */
    static async getCategoryById(id: string): Promise<CategoryModel | null> {
        return await apiRequest<CategoryModel | null>({
            url: `${url}/Category/byId/${id}`,
            method: 'GET'
        });
    }

    /**
     * 获取所有分类下的文章（公开访问）
     * @param id 分类名称
     * @returns Promise<ArticleModel[]> 分类下的文章列表
     */
    static async getCategoryArticles(id: string): Promise<ArticleModel[]> {
        return await apiRequest<ArticleModel[]>({
            url: `${url}/Category/articles/${id}`,
            method: 'GET'
        });
    }

    /**
     * 创建或更新分类
     * @param category 分类信息
     * @returns Promise<void>
     */
    static async createOrUpdateCategory(category: CategoryModel): Promise<void> {
        await apiRequest<void>({
            url: `${url}/Category/CreateOrUpdate`,
            method: 'POST',
            body: JSON.stringify(category)
        });
    }

    /**
     * 删除分类
     * @param name 分类名称
     * @returns Promise<void>
     */
    static async deleteCategory(name: string): Promise<void> {
        await apiRequest<void>({
            url: `${url}/Category/Delete/${encodeURIComponent(name)}`,
            method: 'DELETE'
        });
    }

    /**
     * 更新分类顺序
     * @param name 分类名称
     * @param order 新的顺序值
     * @returns Promise<void>
     */
    static async updateCategoryOrder(name: string, order: number): Promise<void> {
        await apiRequest<void>({
            url: `${url}/Category/UpdateOrder/${name}/${order}`,
            method: 'POST'
        });
    }

    /**
     * 批量更新分类顺序
     * @param categoryOrders 分类名称和对应顺序的字典
     * @returns Promise<void>
     */
    static async updateCategoryOrders(categoryOrders: Record<string, number>): Promise<void> {
        await apiRequest<void>({
            url: `${url}/Category/UpdateOrders`,
            method: 'POST',
            body: JSON.stringify(categoryOrders)
        });
    }
}