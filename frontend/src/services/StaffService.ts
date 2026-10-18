import {url} from './Url';
import {apiRequest} from './ApiService';
import {StaffModel, MemberVO} from "../models";

/**
 * 员工服务类 - 处理员工管理相关的API调用
 */
export class StaffService {
    /**
     * 获取所有员工列表
     * @returns Promise<MemberVO[]> 员工列表（后端 /Staff/members 返回 MemberVO）
     */
    static async getAllStaff(): Promise<MemberVO[]> {
        return apiRequest<MemberVO[]>({
            url: `${url}/Staff/members`,
            method: 'GET'
        });
    }

    /**
     * 根据用户ID获取员工信息
     * @param userId 用户ID
     * @returns Promise<StaffModel> 员工信息
     */
    static async getStaff(userId: string): Promise<StaffModel> {
        return apiRequest<StaffModel>({
            url: `${url}/Staff/${userId}`,
            method: 'GET'
        });
    }

    /**
     * 创建新员工
     * @param staff 员工信息模型
     * @returns Promise<any> 创建结果
     */
    static async createStaff(staff: StaffModel): Promise<any> {
        return apiRequest<any>({
            url: `${url}/Staff/Create`,
            method: 'POST',
            body: staff
        });
    }

    static async updateStaff(staff: StaffModel): Promise<any> {
        return apiRequest<any>({
            // 用户 ID 由请求体中的 staff.userId 绑定；后端路由不包含路径参数。
            url: `${url}/Staff/Update`,
            method: 'POST',
            body: staff
        });
    }

    static async deleteStaff(userId: string): Promise<boolean> {
        await apiRequest<void>({
            url: `${url}/Staff/Delete/${userId}`,
            method: 'GET'
        });
        return true;
    }

    static async getStaffByIdentity(identity: string): Promise<StaffModel[]> {
        return apiRequest<StaffModel[]>({
            url: `${url}/Staff/by-identity/${identity}`,
            method: 'GET'
        });
    }

    static async changeDepartment(userId: string, departmentName: string): Promise<any> {
        return apiRequest<any>({
            url: `${url}/Staff/change-department/${userId}?departmentName=${encodeURIComponent(departmentName)}`,
            method: 'POST'
        });
    }

    /**
     * 人事调动：将已存在的成员调整为部长/社长，或调整其所属部门。
     * identity: Department | Minister | President
     */
    static async assignRole(userId: string, identity: string, departmentName: string | null): Promise<any> {
        return apiRequest<any>({
            url: `${url}/Staff/assign-role`,
            method: 'POST',
            body: {userId, identity, departmentName}
        });
    }
}
