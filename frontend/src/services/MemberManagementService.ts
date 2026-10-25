import {url} from './Url';
import {apiRequest} from './ApiService';
import {StudentCreateDTO, StudentUpdateDTO, StudentVO} from "../models";

/**
 * 成员管理服务类 - 处理成员的删除、新增、更新等管理功能
 */
export class MemberManagementService {
    /**
     * 删除学生成员
     * @param id 学生ID
     * @returns Promise<void>
     */
    static async deleteMember(id: string): Promise<void> {
        await apiRequest<void>({
            url: `${url}/MemberManagement/delete/${id}`,
            method: 'POST'
        });
    }

    /**
     * 新增学生成员
     * 与 update 分开：update 以学号为定位键，学号不存在时必然失败。
     * @param model 学生创建请求（含管理员设置的初始密码）
     * @returns Promise<StudentVO> 新建的学生信息（不含密码字段）
     */
    static async createMember(model: StudentCreateDTO): Promise<StudentVO> {
        return await apiRequest<StudentVO>({
            url: `${url}/MemberManagement/create`,
            method: 'POST',
            body: model
        });
    }

    /**
     * 批量更新或添加学生成员
     * @param list 学生更新请求列表
     * @returns Promise<boolean> 是否成功
     */
    static async updateManyMembers(list: StudentUpdateDTO[]): Promise<boolean> {
        await apiRequest<void>({
            url: `${url}/MemberManagement/update-many`,
            method: 'POST',
            body: list
        });
        return true;
    }

    /**
     * 更新单个成员信息
     * @param model 学生更新请求
     * @returns Promise<void>
     */
    static async updateMember(model: StudentUpdateDTO): Promise<void> {
        await apiRequest<void>({
            url: `${url}/MemberManagement/update`,
            method: 'POST',
            body: model
        });
    }

    /**
     * 管理员重置成员密码
     * @param userId 成员ID
     * @param newPassword 新密码
     * @returns Promise<void>
     */
    static async resetMemberPassword(userId: string, newPassword: string): Promise<void> {
        await apiRequest<void>({
            url: `${url}/MemberManagement/reset-password`,
            method: 'POST',
            body: {userId, newPassword}
        });
    }
}