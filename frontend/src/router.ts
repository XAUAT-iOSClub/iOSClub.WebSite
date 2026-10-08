import { createRouter, createWebHistory } from 'vue-router';
import { useAuthorizationStore } from "./stores/Authorization";
import MainLayout from "./layouts/MainLayout.vue";
import CentreLayout from "./layouts/CentreLayout.vue";

const routes = [
    {
        path: "",
        name: "main",
        component: MainLayout,
        children: [
            {
                path: '',
                name: 'Home',
                meta: { title: "首页 - 西建大 iOS Club" },
                component: () => import('./pages/Home.vue'),
            },
            {
                path: 'projects',
                name: 'Projects',
                meta: { title: "社团项目 - 西建大 iOS Club" },
                component: () => import('./pages/ClubActivities/Projects.vue'),
            },
            {
                path: 'login',
                name: 'Login',
                meta: { title: "登录到您的iMember - 西建大 iOS Club" },
                component: () => import('./pages/LoginSys/Login.vue'),
            },
            {
                path: 'oauth-login',
                name: 'OAuthLogin',
                meta: { title: "OAuth授权登录 - 西建大 iOS Club" },
                component: () => import('./pages/LoginSys/OAuthLogin.vue'),
            },
            {
                path: 'logout',
                name: 'Logout',
                meta: { title: "退出登录 - 西建大 iOS Club" },
                component: () => import('./pages/LoginSys/Logout.vue'),
            },
            {
                path: 'access-denied',
                name: 'AccessDenied',
                meta: { title: "访问被拒绝 - 西建大 iOS Club" },
                component: () => import('./pages/AccessDenied.vue'),
            },
            {
                path: 'signup',
                name: 'SignUp',
                meta: { title: "注册账号 - 西建大 iOS Club" },
                component: () => import('./pages/LoginSys/SignUp.vue'),
            },
            {
                path: 'forgotPassword',
                name: 'ForgotPassword',
                meta: { title: "找回密码 - 西建大 iOS Club" },
                component: () => import('./pages/LoginSys/ForgotPassword.vue'),
            },
            {
                path: 'event',
                name: 'Event',
                meta: { title: "社团活动 - 西建大 iOS Club" },
                component: () => import('./pages/ClubActivities/Event.vue'),
            },
            {
                path: 'History',
                name: 'History',
                meta: { title: "社团历史 - 西建大 iOS Club" },
                component: () => import('./pages/ClubActivities/History.vue'),
            },
            {
                path: 'Tools',
                name: 'Tools',
                meta: { title: "社团应用 - 西建大 iOS Club" },
                component: () => import('./pages/ClubActivities/Tools.vue'),
            },
            {
                path: 'Blog',
                name: 'Blog',
                meta: { title: "社团文章 - 西建大 iOS Club" },
                component: () => import('./pages/ClubActivities/Blog.vue'),
            },
            {
                path: 'QrCode',
                name: 'QrCode',
                meta: { title: "QQ群二维码 - 西建大 iOS Club" },
                component: () => import('./pages/LoginSys/QrCode.vue'),
            },
            {
                path: 'PrivacyPolicy',
                name: 'PrivacyPolicy',
                meta: { title: "隐私政策 - 西建大 iOS Club" },
                component: () => import('./pages/ClubActivities/PrivacyPolicy.vue'),
            },
            {
                path: 'HelpCenter',
                name: 'HelpCenter',
                meta: { title: "帮助中心 - 西建大 iOS Club" },
                component: () => import('./pages/ClubActivities/HelpCenter.vue'),
            },
            {
                path: 'ActivityRegister/:id',
                name: 'ActivityRegister',
                meta: { title: "活动登记 - 西建大 iOS Club" },
                component: () => import('./pages/ActivityRegister.vue'),
            },
            {
                path: '/',
                component: () => import('./layouts/WordLayout.vue'),
                children: [
                    {
                        path: 'About',
                        name: 'About',
                        meta: { title: "关于我们 - 西建大 iOS Club" },
                        component: () => import('./pages/Articles/About.vue'),
                    },
                    {
                        path: 'OtherOrg',
                        name: 'OtherOrg',
                        component: () => import('./pages/Articles/OtherOrg.vue'),
                        meta: {
                            title: '其他组织 - 西建大iOS Club'
                        }
                    },
                    {
                        path: 'Structure',
                        name: 'Structure',
                        meta: { title: "社团结构 - 西建大 iOS Club" },
                        component: () => import('./pages/Articles/Structure.vue'),
                    },
                    {
                        path: 'article/:id',
                        name: 'Article',
                        meta: { title: "文章详情 - 西建大 iOS Club" },
                        component: () => import('./pages/Articles/Article.vue'),
                    },
                ]
            },
            {
                path: '/:pathMatch(.*)*',
                name: 'NotFound',
                meta: { title: "404 - 西建大 iOS Club" },
                component: () => import('./pages/NotFound.vue'),
            }
        ]
    },
    {
        path: '/Centre',
        component: CentreLayout,
        meta: { requiresAuth: true },
        children: [
            {
                path: '',
                name: 'Centre',
                meta: { title: "您的iMember中心 - 西建大 iOS Club", requiresAuth: true },
                component: () => import('./adminPages/Centre.vue'),
            },
            {
                path: 'PersonalData',
                name: 'PersonalData',
                meta: { title: "个人数据 - 西建大 iOS Club" },
                component: () => import('./adminPages/PersonalData.vue'),
            },
            {
                path: 'MemberData',
                name: 'MemberData',
                meta: { title: "成员数据 - 西建大 iOS Club" },
                component: () => import('./adminPages/MemberData.vue'),
            },
            { path: 'FounderPermission', name: 'FounderPermission', meta: { title: '权限管理', requiresAuth: true, founderOnly: true }, component: () => import('./adminPages/FounderPermission.vue') },
            {
                path: 'Department',
                name: 'Department',
                meta: { title: "部门管理 - 西建大 iOS Club" },
                component: () => import('./adminPages/Department.vue'),
            },
            {
                path: 'Category',
                name: 'CategoryManager',
                meta: { title: "分类管理 - 西建大 iOS Club" },
                component: () => import('./adminPages/Category/CategoryManager.vue'),
            },
            {
                path: 'Category/:id',
                name: 'CategorySetting',
                meta: { title: "分类设置 - 西建大 iOS Club" },
                component: () => import('./adminPages/Category/CategorySetting.vue'),
            },
            {
                path: 'Resources',
                name: 'Resources',
                meta: { title: "资源管理 - 西建大 iOS Club" },
                component: () => import('./adminPages/Resources.vue'),
            },
            {
                path: 'Admin',
                name: 'Admin',
                meta: { title: "其他数据 - 西建大 iOS Club" },
                component: () => import('./adminPages/Admin.vue'),
            },
            {
                path: 'Article',
                name: 'AdminArticle',
                meta: { title: "社团文章 - 西建大 iOS Club" },
                component: () => import('./adminPages/Article/ArticleManager.vue'),
            },
            {
                path: 'Article/edit/:path?',
                name: 'ArticleEditor',
                meta: { title: "编辑文章 - 西建大 iOS Club" },
                component: () => import('./adminPages/Article/ArticleEditor.vue'),
            },
            {
                path: 'Client',
                name: 'ClientApplication',
                meta: { title: "客户端应用 - 西建大 iOS Club" },
                component: () => import('./adminPages/ClientApplication.vue'),
            },
            {
                path: 'Logs',
                name: 'Logs',
                meta: { title: "日志查看 - 西建大 iOS Club" },
                component: () => import('./adminPages/LogsViewer.vue'),
            },
            {
                path: 'IpBlacklist',
                name: 'IpBlacklist',
                meta: { title: "IP黑名单管理 - 西建大 iOS Club" },
                component: () => import('./adminPages/IpBlacklist.vue'),
            },
            {
                path: 'Activity',
                name: 'ActivityManager',
                meta: { title: "活动记录 - 西建大 iOS Club", requiresAuth: true },
                component: () => import('./adminPages/Activity/ActivityManager.vue'),
            },
            {
                path: 'Activity/:id',
                name: 'ActivityDetail',
                meta: { title: "活动详情 - 西建大 iOS Club", requiresAuth: true },
                component: () => import('./adminPages/Activity/ActivityDetail.vue'),
            },
        ]
    }
];

const router = createRouter({
    history: createWebHistory(),
    routes,
});

// Navigation guard
router.beforeEach((to, _from, next) => {
    const authorizationStore = useAuthorizationStore();

    // Set page title
    document.title = (to.meta.title as string) || "西建大 iOS Club";

    // Check if route requires authentication — also validate JWT expiry
    if (to.meta.requiresAuth && !authorizationStore.isTokenValid) {
        next('/login');
    } else if (to.meta.founderOnly && authorizationStore.getRole !== 'Founder') {
        next('/access-denied');
    } else {
        next();
    }
});

export default router;
