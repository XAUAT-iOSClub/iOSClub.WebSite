<script setup lang="ts">
import {ref, computed, h, onMounted, onBeforeUnmount, nextTick, defineComponent} from 'vue'
import {useDialog, useMessage} from 'naive-ui'
import {
  NSelect,
  NInput,
  NDataTable,
  NModal,
  NForm,
  NFormItem,
  NRadioGroup,
  NSpace,
  NRadio,
  NNumberAnimation,
  NTag
} from 'naive-ui'
import {Icon} from '@iconify/vue'
import {MemberQueryService} from '../services/MemberQueryService'
import {MemberManagementService} from '../services/MemberManagementService'
import {InfoService} from '../services/InfoService'
import {
  DataCentreService
} from '../services/DataCentreService'
import type {MemberVO, StudentUpdateDTO, PaginatedMemberResponse} from '../models'
import type {
  AcademyCount,
  GenderCount,
  GradeCount,
  PoliticalCount,
  YearCount
} from '../services/DataCentreService'
import * as echarts from 'echarts'
import {useLayoutStore} from '../stores/LayoutStore';
import {filterInSchool} from '../lib/memberFilter';
import {TableColumn} from "naive-ui/es/data-table/src/interface";

// --- Types & Interfaces ---

const dialog = useDialog()
const message = useMessage()
const layoutStore = useLayoutStore()

// --- State ---
const showModal = ref(false)
const showPasswordModal = ref(false)
const searchTerm = ref('')
const searchItem = ref('username') // Default value matching options
const formRef = ref<any>(null)
const passwordFormRef = ref<any>(null)
const loading = ref(false)
const activeTab = ref('memberData')

const fileInput = ref<HTMLInputElement>()

// Pagination
const currentPage = ref(1)
const pageSize = ref(10)
const totalCount = ref(0)

// Dropdown
const dropdownOpen = ref(false)

// Chart Refs
const yearChartRef = ref<HTMLDivElement | null>(null)
const collegeChartRef = ref<HTMLDivElement | null>(null)
const gradeChartRef = ref<HTMLDivElement | null>(null)
const genderChartRef = ref<HTMLDivElement | null>(null)
const politicalChartRef = ref<HTMLDivElement | null>(null)

// Chart Instances
let yearChart: echarts.ECharts | null = null
let collegeChart: echarts.ECharts | null = null
let gradeChart: echarts.ECharts | null = null
let genderChart: echarts.ECharts | null = null
let politicalChart: echarts.ECharts | null = null

// --- Options ---
const searchItems = [
  {label: '姓名', value: 'username'},
  {label: '学号', value: 'userid'},
  {label: '学院', value: 'academy'},
  {label: '专业班级', value: 'classname'},
  {label: '手机号', value: 'phone_num'}
]

const tabs = [
  {label: '成员列表', value: 'memberData', icon: 'ion:list'},
  {label: '历年趋势', value: 'yearData', icon: 'ion:trending-up'},
  {label: '学院分布', value: 'collegeData', icon: 'ion:school'},
  {label: '年级构成', value: 'gradeData', icon: 'ion:stats-bars'},
  {label: '性别比例', value: 'genderData', icon: 'ion:male-female'},
  {label: '政治面貌', value: 'politicalData', icon: 'ion:flag'},
]

// 学院列表由后端 /Info/academies 提供，避免与 SignRecord.Academies 校验漂移
const academyOptions = ref<{ label: string; value: string }[]>([])

const fetchAcademyOptions = async () => {
  try {
    const academies = await InfoService.getAcademies()
    academyOptions.value = academies.map(academy => ({label: academy, value: academy}))
  } catch (error: any) {
    message.error(error.message || '获取学院列表失败')
  }
}

const politicalLandscapeOptions = ['群众', '共青团员', '中共党员'].map(p => ({label: p, value: p}))
const genderOptions = ['男', '女']

// --- Data Models ---
const members = ref<MemberVO[]>([])
const currentMember = ref<MemberVO>({
  identity: '',
  userName: '',
  userId: '',
  academy: '',
  className: '',
  phoneNum: '',
  politicalLandscape: '',
  gender: '',
  joinTime: '',
  eMail: null
} as MemberVO)

const passwordForm = ref({
  newPassword: '',
  confirmPassword: ''
})

// --- Validation Rules ---
const rules = {
  userName: {required: true, message: '请输入姓名', trigger: 'blur'},
  userId: [
    {required: true, message: '请输入学号', trigger: 'blur'},
    {min: 10, max: 10, message: '学号应为10位数字', trigger: 'blur'}
  ],
  academy: {required: true, message: '请选择学院', trigger: 'change'},
  className: {required: true, message: '请输入专业班级', trigger: 'blur'},
  phoneNum: [
    {required: true, message: '请输入手机号', trigger: 'blur'},
    {pattern: /^1[3-9]\d{9}$/, message: '请输入正确的手机号', trigger: 'blur'}
  ],
  politicalLandscape: {required: true, message: '请选择政治面貌', trigger: 'change'},
  gender: {required: true, message: '请选择性别', trigger: 'change'}
}

const passwordRules = {
  newPassword: [
    {required: true, message: '请输入新密码', trigger: 'blur'},
    {min: 6, message: '密码长度至少6位', trigger: 'blur'}
  ],
  confirmPassword: [
    {required: true, message: '请确认密码', trigger: 'blur'},
    {
      validator: (_: any, value: string) => value === passwordForm.value.newPassword,
      message: '两次输入的密码不一致',
      trigger: 'blur'
    }
  ]
}

// --- Table Columns ---
const columns = [
  {title: '姓名', key: 'userName', width: 100, fixed: 'left'},
  {title: '学号', key: 'userId', width: 120},
  {title: '学院', key: 'academy', width: 180, ellipsis: {tooltip: true}},
  {title: '专业', key: 'className', width: 140, ellipsis: {tooltip: true}},
  {title: '手机', key: 'phoneNum', width: 130},
  {
    title: '面貌',
    key: 'politicalLandscape',
    width: 100,
    render(row: MemberVO) {
      let type = 'default';
      if (row.politicalLandscape === '中共党员') type = 'error';
      if (row.politicalLandscape === '共青团员') type = 'info';
      return h(NTag, {
        type: type as any,
        size: 'small',
        bordered: false,
        round: true
      }, {default: () => row.politicalLandscape});
    }
  },
  {title: '性别', key: 'gender', width: 70},
  {
    title: '操作',
    key: 'actions',
    width: 180,
    render(row: MemberVO) {
      // Using standard HTML/Tailwind buttons via h function for cleaner look instead of NButton
      const btnClass = "px-3 py-1 text-xs font-medium rounded-full transition-colors duration-200 mx-1";

      return [
        h('button', {
          class: `${btnClass} text-blue-600 bg-blue-50 hover:bg-blue-100 dark:text-blue-400 dark:bg-blue-900/30 dark:hover:bg-blue-900/50`,
          onClick: () => editMember(row)
        }, '编辑'),
        h('button', {
          class: `${btnClass} text-amber-600 bg-amber-50 hover:bg-amber-100 dark:text-amber-400 dark:bg-amber-900/30 dark:hover:bg-amber-900/50`,
          onClick: () => showPasswordModalFn(row)
        }, '改密'),
        h('button', {
          class: `${btnClass} text-red-600 bg-red-50 hover:bg-red-100 dark:text-red-400 dark:bg-red-900/30 dark:hover:bg-red-900/50`,
          onClick: () => deleteMember(row)
        }, '删除')
      ]
    }
  }
] as TableColumn<MemberVO>[]

const paginationConfig = computed(() => ({
  page: currentPage.value,
  pageSize: pageSize.value,
  showSizePicker: true,
  pageSizes: [10, 20, 30, 50],
  itemCount: totalCount.value,
}))

// --- Chart Data State ---
const yearData = ref<YearCount[]>([])
const collegeData = ref<AcademyCount[]>([])
const gradeData = ref<GradeCount[]>([])
const genderData = ref<GenderCount[]>([])
const politicalData = ref<PoliticalCount[]>([])

// --- Logic ---

const onChange = (page: number) => {
  currentPage.value = page
  fetchMembers()
}

const onUpdatePageSize = (size: number) => {
  pageSize.value = size
  currentPage.value = 1
  fetchMembers()
}

const loadChartData = async () => {
  try {
    loading.value = true
    const [yearResult, collegeResult, gradeResult, genderResult, landscapeResult] = await Promise.all([
      DataCentreService.getYearData(),
      DataCentreService.getCollegeData(),
      DataCentreService.getGradeData(),
      DataCentreService.getGenderData(),
      DataCentreService.getLandscapeData()
    ])
    yearData.value = yearResult
    collegeData.value = collegeResult
    gradeData.value = gradeResult.map(item => ({grade: item.grade, value: item.value}))
    genderData.value = genderResult
    politicalData.value = landscapeResult.map(item => ({type: item.type, value: item.value}))

    if (activeTab.value !== 'memberData') handleTabChange(activeTab.value)
  } catch (error: any) {
    message.error('获取图表数据失败: ' + (error.message || '未知错误'))
  } finally {
    loading.value = false
  }
}

const fetchMembers = async () => {
  try {
    loading.value = true
    const response: PaginatedMemberResponse = await MemberQueryService.getAllDataByPage(
        currentPage.value,
        pageSize.value,
        searchTerm.value,
        searchItem.value
    )
    members.value = response.data
    totalCount.value = response.totalCount
  } catch (error: any) {
    message.error('获取成员数据出错: ' + (error.message || '未知错误'))
  } finally {
    loading.value = false
  }
}

// Chart Renders (Use system colors for ECharts to match themes later if needed, simple colors for now)
const initChart = (chartRef: HTMLDivElement | null, option: any) => {
  if (chartRef) {
    const chart = echarts.init(chartRef)
    chart.setOption(option)
    return chart
  }
  return null
}

const renderYearChart = () => {
  if (!yearChartRef.value) return
  const isDark = document.documentElement.classList.contains('dark')
  const textColor = isDark ? '#e5e7eb' : '#374151'

  yearChart = initChart(yearChartRef.value, {
    backgroundColor: 'transparent',
    tooltip: {trigger: 'axis'},
    grid: {top: 30, right: 20, bottom: 30, left: 40, containLabel: true},
    xAxis: {type: 'category', data: yearData.value.map(item => item.year), axisLabel: {color: textColor}},
    yAxis: {
      type: 'value',
      splitLine: {lineStyle: {type: 'dashed', color: isDark ? '#374151' : '#e5e7eb'}},
      axisLabel: {color: textColor}
    },
    series: [{
      data: yearData.value.map(item => item.value),
      type: 'line',
      smooth: true,
      symbol: 'circle',
      symbolSize: 8,
      itemStyle: {color: '#007AFF'},
      areaStyle: {
        color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
          {offset: 0, color: 'rgba(0, 122, 255, 0.5)'},
          {offset: 1, color: 'rgba(0, 122, 255, 0.0)'}
        ])
      }
    }]
  })
}

const renderCollegeChart = () => {
  if (!collegeChartRef.value) return
  const isDark = document.documentElement.classList.contains('dark')
  const textColor = isDark ? '#e5e7eb' : '#374151'

  collegeChart = initChart(collegeChartRef.value, {
    backgroundColor: 'transparent',
    tooltip: {trigger: 'item'},
    legend: {bottom: '0%', textStyle: {color: textColor}},
    series: [{
      type: 'pie',
      radius: ['40%', '70%'],
      center: ['50%', '45%'],
      itemStyle: {borderRadius: 8, borderColor: isDark ? '#1C1C1E' : '#fff', borderWidth: 2},
      data: collegeData.value.map(item => ({name: item.type, value: item.value})),
      emphasis: {itemStyle: {shadowBlur: 10, shadowOffsetX: 0, shadowColor: 'rgba(0, 0, 0, 0.5)'}}
    }]
  })
}

const renderGradeChart = () => {
  if (!gradeChartRef.value) return
  const isDark = document.documentElement.classList.contains('dark')
  const textColor = isDark ? '#e5e7eb' : '#374151'

  gradeChart = initChart(gradeChartRef.value, {
    backgroundColor: 'transparent',
    tooltip: {trigger: 'axis', axisPointer: {type: 'shadow'}},
    grid: {containLabel: true, left: 10, right: 10, bottom: 10, top: 30},
    xAxis: {type: 'category', data: gradeData.value.map(item => item.grade), axisLabel: {color: textColor}},
    yAxis: {type: 'value', show: false},
    series: [{
      data: gradeData.value.map(item => item.value),
      type: 'bar',
      barWidth: '40%',
      itemStyle: {color: '#34C759', borderRadius: [4, 4, 0, 0]},
      label: {show: true, position: 'top', color: textColor}
    }]
  })
}

const renderGenderChart = () => {
  if (!genderChartRef.value) return
  const isDark = document.documentElement.classList.contains('dark')
  const textColor = isDark ? '#e5e7eb' : '#374151'

  genderChart = initChart(genderChartRef.value, {
    backgroundColor: 'transparent',
    tooltip: {trigger: 'item'},
    legend: {bottom: '0%', textStyle: {color: textColor}},
    color: ['#007AFF', '#FF2D55'],
    series: [{
      type: 'pie',
      radius: ['50%', '80%'],
      center: ['50%', '45%'],
      itemStyle: {borderRadius: 8, borderColor: isDark ? '#1C1C1E' : '#fff', borderWidth: 2},
      data: genderData.value.map(item => ({name: item.type, value: item.value}))
    }]
  })
}

const renderPoliticalChart = () => {
  if (!politicalChartRef.value) return
  const isDark = document.documentElement.classList.contains('dark')
  const textColor = isDark ? '#e5e7eb' : '#374151'

  politicalChart = initChart(politicalChartRef.value, {
    backgroundColor: 'transparent',
    tooltip: {trigger: 'axis', axisPointer: {type: 'shadow'}},
    grid: {containLabel: true, left: 10, right: 10, bottom: 10, top: 30},
    xAxis: {type: 'category', data: politicalData.value.map(item => item.type), axisLabel: {color: textColor}},
    yAxis: {type: 'value', show: false},
    series: [{
      data: politicalData.value.map(item => item.value),
      type: 'bar',
      barWidth: '40%',
      itemStyle: {color: '#AF52DE', borderRadius: [4, 4, 0, 0]},
      label: {show: true, position: 'top', color: textColor}
    }]
  })
}

const handleTabChange = (val: string) => {
  activeTab.value = val
  nextTick(() => {
    // Small delay to ensure container is ready
    setTimeout(() => {
      switch (val) {
        case 'yearData':
          renderYearChart();
          break;
        case 'collegeData':
          renderCollegeChart();
          break;
        case 'gradeData':
          renderGradeChart();
          break;
        case 'genderData':
          renderGenderChart();
          break;
        case 'politicalData':
          renderPoliticalChart();
          break;
      }
    }, 50)
  })
}

// --- CRUD Operations ---
const showAddMemberModal = () => {
  currentMember.value = {
    identity: '', userName: '', userId: '', academy: '', className: '',
    phoneNum: '', politicalLandscape: '', gender: '',
    joinTime: new Date().toISOString(), eMail: null
  } as MemberVO
  showModal.value = true
}

const editMember = (member: MemberVO) => {
  currentMember.value = {...member}
  showModal.value = true
}

const deleteMember = async (member: MemberVO) => {
  dialog.warning({
    title: '确认删除',
    content: `确定要删除成员 ${member.userName} 吗？`,
    positiveText: '确定',
    negativeText: '取消',
    onPositiveClick: async () => {
      try {
        await MemberManagementService.deleteMember(member.userId)
        const index = members.value.findIndex(m => m.userId === member.userId)
        if (index !== -1) {
          members.value.splice(index, 1)
          message.success(`已删除`)
          totalCount.value--
        }
      } catch (error: any) {
        message.error(error.message || '删除失败')
      }
    }
  })
}

const saveMember = async () => {
  formRef.value?.validate(async (errors: any) => {
    if (!errors) {
      try {
        // 只提交后端 StudentUpdateDTO 接受的字段；
        // identity / joinTime 不在该 DTO 里，发过去会被静默忽略。
        const payload: StudentUpdateDTO = {
          userId: currentMember.value.userId,
          userName: currentMember.value.userName,
          academy: currentMember.value.academy,
          politicalLandscape: currentMember.value.politicalLandscape,
          gender: currentMember.value.gender,
          className: currentMember.value.className,
          phoneNum: currentMember.value.phoneNum,
          eMail: currentMember.value.eMail
        }
        if (currentMember.value.identity) {
          await MemberManagementService.updateMember(payload)
          const index = members.value.findIndex(m => m.identity === currentMember.value.identity)
          if (index !== -1) members.value[index] = {...currentMember.value}
          message.success('已更新')
        } else {
          await MemberManagementService.updateMember(payload)
          message.success('已添加')
          await fetchMembers() // Refresh for ID generation if real backend
        }
        showModal.value = false
      } catch (error: any) {
        message.error(error.message || '保存失败')
      }
    }
  })
}

const searchMembers = () => {
  currentPage.value = 1
  fetchMembers()
}

// --- Files & Export ---
const triggerFileInput = () => fileInput.value?.click()

const updateMemberUseJson = async (event: Event) => {
  const target = event.target as HTMLInputElement
  const files = target.files
  if (!files || !files.length) return
  try {
    const data = await files[0]?.text().then(x => JSON.parse(x))
    if (!data) throw new Error('Empty data')
    const result = await MemberManagementService.updateManyMembers(data)
    if (result) {
      message.success('数据更新成功')
      await fetchMembers()
    }
  } catch (e: any) {
    message.error('导入失败')
  }
}

// 两种导出模式：normal = 数据库全部成员；school = 仅在校成员（学号前两位晚于 本年-4）
const handleDownloadSelect = async (key: string) => {
  const inSchoolOnly = key.startsWith('school:')
  const format = key.endsWith('json') ? 'json' : 'csv'
  const allMembers = await MemberQueryService.getAllData();
  const list = inSchoolOnly ? filterInSchool(allMembers) : allMembers
  const suffix = inSchoolOnly ? '-在校成员' : ''
  if (format === 'json') {
    const blob = new Blob([JSON.stringify(list, null, 2)], {type: 'application/json'})
    downloadBlob(blob, `members${suffix}.json`)
  } else {
    const headers = ['姓名', '学号', '学院', '专业班级', '手机号', '政治面貌', '性别']
    const keys = ['userName', 'userId', 'academy', 'className', 'phoneNum', 'politicalLandscape', 'gender']
    let csv = '\uFEFF' + headers.join(',') + '\n'
    list.forEach(m => {
      csv += keys.map(k => `"${(m as any)[k] || ''}"`).join(',') + '\n'
    })
    downloadBlob(new Blob([csv], {type: 'text/csv'}), `members${suffix}.csv`)
  }
  message.success('导出成功')
}

const downloadBlob = (blob: Blob, filename: string) => {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = filename
  link.click()
  URL.revokeObjectURL(url)
}

// --- Password ---
const showPasswordModalFn = (member: MemberVO) => {
  currentMember.value = {...member}
  passwordForm.value = {newPassword: '', confirmPassword: ''}
  showPasswordModal.value = true
}

const changePassword = async () => {
  passwordFormRef.value?.validate(async (errors: any) => {
    if (!errors) {
      try {
        await MemberManagementService.resetMemberPassword(currentMember.value.userId, passwordForm.value.newPassword)
        message.success('密码已重置')
        showPasswordModal.value = false
      } catch (e: any) {
        message.error(e.message)
      }
    }
  })
}

// --- Lifecycle ---
const handleResize = () => {
  if (yearChart) yearChart.resize()
  if (collegeChart) collegeChart.resize()
  if (gradeChart) gradeChart.resize()
  if (genderChart) genderChart.resize()
  if (politicalChart) politicalChart.resize()
}

onMounted(() => {
  fetchMembers()
  fetchAcademyOptions()
  loadChartData()
  window.addEventListener('resize', handleResize)

  layoutStore.setPageHeader('成员中心', '管理社团成员信息与数据可视化')
  layoutStore.setShowPageActions(true)

  // Apple-style Action Bar
  const ActionsComponent = defineComponent({
    setup() {
      return () => h('div', {class: 'flex items-center gap-3'}, [
        h('button', {
          class: 'bg-white dark:bg-[#2C2C2E] text-[#1d1d1f] dark:text-white font-medium rounded-full border border-gray-200 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-[#3A3A3C] transition-all duration-200 active:scale-95 flex items-center justify-center h-9 px-4 text-[14px]',
          onClick: () => dropdownOpen.value = !dropdownOpen.value
        }, [
          h(Icon, {icon: 'ion:download-outline', class: 'w-5 h-5 mr-1'}),
          '导出'
        ]),

        // Dropdown menu (manual implementation for style control)
        dropdownOpen.value ? h('div', {class: 'absolute top-12 right-0 w-44 bg-white/90 dark:bg-[#2C2C2E]/90 backdrop-blur-xl rounded-xl shadow-[0_8px_24px_rgba(0,0,0,0.12)] border border-white/20 dark:border-white/10 p-1 z-50 flex flex-col animate-in fade-in zoom-in-95 duration-200 origin-top-right'}, [
          h('button', {
            class: 'text-left px-3 py-2 text-sm rounded-lg hover:bg-black/5 dark:hover:bg-white/10 transition-colors',
            onClick: () => {
              handleDownloadSelect('csv');
              dropdownOpen.value = false
            }
          }, '导出 CSV'),
          h('button', {
            class: 'text-left px-3 py-2 text-sm rounded-lg hover:bg-black/5 dark:hover:bg-white/10 transition-colors',
            onClick: () => {
              handleDownloadSelect('json');
              dropdownOpen.value = false
            }
          }, '导出 JSON'),
          h('div', {class: 'my-1 border-t border-black/5 dark:border-white/10'}),
          h('button', {
            class: 'text-left px-3 py-2 text-sm rounded-lg hover:bg-black/5 dark:hover:bg-white/10 transition-colors',
            onClick: () => {
              handleDownloadSelect('school:csv');
              dropdownOpen.value = false
            }
          }, '在校成员 CSV'),
          h('button', {
            class: 'text-left px-3 py-2 text-sm rounded-lg hover:bg-black/5 dark:hover:bg-white/10 transition-colors',
            onClick: () => {
              handleDownloadSelect('school:json');
              dropdownOpen.value = false
            }
          }, '在校成员 JSON')
        ]) : null,

        h('button', {
          class: 'bg-white dark:bg-[#2C2C2E] text-[#1d1d1f] dark:text-white font-medium rounded-full border border-gray-200 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-[#3A3A3C] transition-all duration-200 active:scale-95 flex items-center justify-center h-9 px-4 text-[14px]',
          onClick: triggerFileInput
        }, [
          h(Icon, {icon: 'ion:cloud-upload-outline', class: 'w-5 h-5 mr-1'}),
          '导入'
        ]),

        h('button', {
          class: 'bg-[#007AFF] hover:bg-[#0062CC] text-white font-medium rounded-full transition-all duration-200 shadow-sm active:scale-95 flex items-center justify-center h-9 px-4 text-[14px]',
          onClick: showAddMemberModal
        }, [
          h(Icon, {icon: 'ion:add', class: 'w-5 h-5 mr-1'}),
          '添加成员'
        ])
      ])
    }
  })

  layoutStore.setActionsComponent(ActionsComponent)
})

onBeforeUnmount(() => {
  layoutStore.clearPageHeader()
  window.removeEventListener('resize', handleResize)
})
</script>

<template>
  <!-- Global Background Container -->
  <div
      class="min-h-screen bg-[#F5F5F7] dark:bg-[#000000] pb-10 transition-colors duration-300 text-[15px] text-[#1d1d1f] dark:text-[#f5f5f7]">

    <input ref="fileInput" type="file" accept=".json" @change="updateMemberUseJson" class="hidden"/>

    <div class="max-w-[1400px] mx-auto px-4 sm:px-6 lg:px-8 py-6">

      <!-- iOS Segmented Control (Tabs) -->
      <div class="flex justify-center mb-8">
        <div
            class="bg-[#E3E3E8]/80 dark:bg-[#1C1C1E]/80 backdrop-blur-md p-1 rounded-xl inline-flex gap-1 shadow-inner overflow-x-auto max-w-full scrollbar-hide">
          <button
              v-for="tab in tabs"
              :key="tab.value"
              @click="handleTabChange(tab.value)"
              class="relative px-4 py-1.5 rounded-[9px] text-sm font-medium transition-all duration-300 ease-out whitespace-nowrap flex items-center gap-2"
              :class="activeTab === tab.value ? 'bg-white dark:bg-[#636366] shadow-[0_1px_2px_rgba(0,0,0,0.12)] text-black dark:text-white' : 'text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-200'"
          >
            <Icon :icon="tab.icon" class="text-lg opacity-80"/>
            {{ tab.label }}
          </button>
        </div>
      </div>

      <!-- Content Area -->
      <div v-show="activeTab === 'memberData'" class="space-y-4">
        <!-- Tool Bar (Search) -->
        <div class="glass-panel p-2 flex flex-col md:flex-row items-center gap-2 rounded-2xl">
          <div class="w-full md:w-48">
            <!-- Using Naive UI but scoped styling wrapper for visual consistency -->
            <n-select
                v-model:value="searchItem"
                :options="searchItems"
                class="custom-select"
            />
          </div>

          <div class="flex-1 w-full relative group">
            <div class="absolute inset-y-0 left-3 flex items-center pointer-events-none text-gray-400">
              <Icon icon="ion:search" class="w-5 h-5"/>
            </div>
            <n-input
                v-model:value="searchTerm"
                placeholder="搜索成员..."
                class="custom-input pl-8"
                @keyup.enter="searchMembers"
            />
          </div>

          <div class="hidden md:flex items-center px-4 text-sm text-gray-500">
            <span class="mr-2">共</span>
            <n-number-animation :from="0" :to="totalCount" class="font-semibold text-black dark:text-white text-lg"/>
            <span class="ml-2">人</span>
          </div>

          <button @click="searchMembers" class="ios-btn-primary w-full md:w-auto px-6 py-2 !rounded-xl">
            搜索
          </button>
        </div>

        <!-- Table Card -->
        <div
            class="bg-white dark:bg-[#1C1C1E] rounded-2xl shadow-sm border border-black/5 dark:border-white/5 overflow-hidden transition-colors">
          <div class="min-h-[500px]">
            <n-data-table
                remote
                :columns="columns"
                :data="members"
                :pagination="paginationConfig"
                @update:page="onChange"
                @update:page-size="onUpdatePageSize"
                :bordered="false"
                :loading="loading"
                :single-line="false"
                size="large"
                class="ios-table mb-2"
            />
          </div>
        </div>
      </div>

      <!-- Chart Views (Grid Layout) -->
      <div v-show="activeTab !== 'memberData'"
           class="grid grid-cols-1 md:grid-cols-1 gap-6 animate-in fade-in duration-500 slide-in-from-bottom-4">
        <div
            class="bg-white dark:bg-[#1C1C1E] p-6 rounded-3xl shadow-[0_4px_20px_rgba(0,0,0,0.04)] border border-black/5 dark:border-white/5">
          <!-- Chart Header -->
          <div class="flex items-center justify-between mb-6">
            <h3 class="text-xl font-bold tracking-tight">
              {{ tabs.find(t => t.value === activeTab)?.label }}
            </h3>
            <div class="p-2 bg-gray-100 dark:bg-gray-800 rounded-full">
              <Icon :icon="tabs.find(t => t.value === activeTab)?.icon || ''" class="w-6 h-6 text-gray-500"/>
            </div>
          </div>

          <!-- Chart Containers -->
          <div class="h-[400px] w-full relative">
            <div v-if="loading" class="absolute inset-0 flex items-center justify-center backdrop-blur-sm z-10">
              <Icon icon="svg-spinners:90-ring-with-bg" class="w-10 h-10 text-blue-500"/>
            </div>

            <div v-show="activeTab === 'yearData'" ref="yearChartRef" class="h-full w-full"/>
            <div v-show="activeTab === 'collegeData'" ref="collegeChartRef" class="h-full w-full"/>
            <div v-show="activeTab === 'gradeData'" ref="gradeChartRef" class="h-full w-full"/>
            <div v-show="activeTab === 'genderData'" ref="genderChartRef" class="h-full w-full"/>
            <div v-show="activeTab === 'politicalData'" ref="politicalChartRef" class="h-full w-full"/>
          </div>
        </div>
      </div>

    </div>

    <!-- Modals (Customized Presets) -->
    <n-modal v-model:show="showModal" preset="card" class="custom-modal w-full max-w-lg"
             :title="currentMember.identity ? '编辑成员' : '添加成员'">
      <n-form :model="currentMember" :rules="rules" ref="formRef" label-placement="left" label-width="auto"
              require-mark-placement="right-hanging">
        <div class="grid grid-cols-1 gap-4 py-2">
          <n-form-item label="姓名" path="userName">
            <n-input v-model:value="currentMember.userName" class="custom-input" placeholder="姓名"/>
          </n-form-item>
          <n-form-item label="学号" path="userId">
            <n-input v-model:value="currentMember.userId" class="custom-input" placeholder="10位学号"/>
          </n-form-item>
          <n-form-item label="学院" path="academy">
            <n-select v-model:value="currentMember.academy" :options="academyOptions" filterable class="custom-select"/>
          </n-form-item>
          <n-form-item label="班级" path="className">
            <n-input v-model:value="currentMember.className" class="custom-input" placeholder="如: 计科2301"/>
          </n-form-item>
          <n-form-item label="手机" path="phoneNum">
            <n-input v-model:value="currentMember.phoneNum" class="custom-input"/>
          </n-form-item>
          <n-form-item label="面貌" path="politicalLandscape">
            <n-select v-model:value="currentMember.politicalLandscape" :options="politicalLandscapeOptions"
                      class="custom-select"/>
          </n-form-item>
          <n-form-item label="性别" path="gender">
            <n-radio-group v-model:value="currentMember.gender">
              <n-space>
                <n-radio v-for="g in genderOptions" :key="g" :value="g">{{ g }}</n-radio>
              </n-space>
            </n-radio-group>
          </n-form-item>
          <n-form-item label="邮箱" path="eMail">
            <n-input v-model:value="currentMember.eMail" class="custom-input"/>
          </n-form-item>
        </div>
      </n-form>
      <template #footer>
        <div class="flex justify-end gap-3">
          <button class="ios-btn-secondary px-6" @click="showModal = false">取消</button>
          <button class="ios-btn-primary px-6" @click="saveMember">保存</button>
        </div>
      </template>
    </n-modal>

    <n-modal v-model:show="showPasswordModal" preset="card" class="custom-modal w-full max-w-md" title="重置密码">
      <n-form :model="passwordForm" :rules="passwordRules" ref="passwordFormRef" class="py-4">
        <div class="bg-gray-50 dark:bg-white/5 p-4 rounded-xl mb-4">
          <p class="text-xs text-gray-500 mb-1">正在为以下用户修改密码:</p>
          <p class="font-medium">{{ currentMember.userName }} ({{ currentMember.userId }})</p>
        </div>
        <n-form-item label="新密码" path="newPassword">
          <n-input v-model:value="passwordForm.newPassword" type="password" show-password-on="click"
                   class="custom-input"/>
        </n-form-item>
        <n-form-item label="确认密码" path="confirmPassword">
          <n-input v-model:value="passwordForm.confirmPassword" type="password" show-password-on="click"
                   class="custom-input"/>
        </n-form-item>
      </n-form>
      <template #footer>
        <div class="flex justify-end gap-3">
          <button class="ios-btn-secondary px-6" @click="showPasswordModal = false">取消</button>
          <button class="ios-btn-primary px-6 bg-amber-500 hover:bg-amber-600" @click="changePassword">确认重置</button>
        </div>
      </template>
    </n-modal>

  </div>
</template>

<style scoped>
/* === Apple Style Components === */

/* Glassmorphism panel helpers */
.glass-panel {
  background: rgba(255, 255, 255, 0.7);
  backdrop-filter: blur(20px);
  /* border: 1px solid rgba(0,0,0,0.05); */
}

.dark .glass-panel {
  background: rgba(28, 28, 30, 0.7);
  border-color: rgba(255, 255, 255, 0.1);
}

/* iOS Button Styles */
.ios-btn-primary {
  background-color: #007AFF;
  color: white;
  font-weight: 500;
  border-radius: 9999px;
  transition: all 200ms cubic-bezier(0.4, 0, 0.2, 1);
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.05);
  display: flex;
  align-items: center;
  justify-content: center;
  height: 2.25rem;
  padding-left: 1rem;
  padding-right: 1rem;
  font-size: 14px;
}

.ios-btn-primary:hover {
  background-color: #0062CC;
}

.ios-btn-primary:active {
  transform: scale(0.95);
}

.ios-btn-secondary {
  background-color: white;
  color: #1d1d1f;
  font-weight: 500;
  border-radius: 9999px;
  border: 1px solid #e5e7eb;
  transition: all 200ms cubic-bezier(0.4, 0, 0.2, 1);
  display: flex;
  align-items: center;
  justify-content: center;
  height: 2.25rem;
  padding-left: 1rem;
  padding-right: 1rem;
  font-size: 14px;
}

.ios-btn-secondary:hover {
  background-color: #f9fafb;
}

.ios-btn-secondary:active {
  transform: scale(0.95);
}

.dark .ios-btn-secondary {
  background-color: #2C2C2E;
  color: white;
  border-color: #374151;
}

.dark .ios-btn-secondary:hover {
  background-color: #3A3A3C;
}

/* Hide Scrollbar */
.scrollbar-hide::-webkit-scrollbar {
  display: none;
}

.scrollbar-hide {
  -ms-overflow-style: none;
  scrollbar-width: none;
}

/* Naive UI Overrides for iOS Look */

/* Modal Styling */
:deep(.custom-modal) {
  border-radius: 24px !important;
  background-color: rgba(255, 255, 255, 0.95) !important;
  backdrop-filter: blur(20px);
  border: 1px solid rgba(0, 0, 0, 0.05);
  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.15) !important;
}

.dark .custom-modal {
  background-color: rgba(28, 28, 30, 0.95) !important;
  border: 1px solid rgba(255, 255, 255, 0.1) !important;
}

:deep(.n-card-header__main) {
  font-weight: 700 !important;
  font-size: 1.1rem !important;
  text-align: center;
}

/* Input & Select Styling Overrides */
:deep(.n-input .n-input__border),
:deep(.n-input:hover .n-input__border),
:deep(.n-input.n-input--focus .n-input__border) {
  border: 1px solid transparent !important;
  box-shadow: none !important;
}

:deep(.n-input) {
  background-color: rgba(118, 118, 128, 0.12) !important; /* iOS Search Bar Gray */
  border-radius: 10px !important;
}

:deep(.dark) .n-input {
  background-color: rgba(118, 118, 128, 0.24) !important;
}

:deep(.n-base-selection) {
  background-color: rgba(118, 118, 128, 0.12) !important;
  border-color: transparent !important;
  border-radius: 10px !important;
  --n-border-active: transparent !important;
  --n-box-shadow-active: none !important;
  --n-box-shadow-focus: none !important;
}

:deep(.dark) .n-base-selection {
  background-color: rgba(118, 118, 128, 0.24) !important;
}

/* Table Styling */
:deep(.ios-table .n-data-table-th) {
  background-color: transparent !important;
  border-bottom: 1px solid rgba(0, 0, 0, 0.05) !important;
  font-weight: 600;
  color: #86868b;
}

:deep(.dark) .ios-table .n-data-table-th {
  border-bottom: 1px solid rgba(255, 255, 255, 0.1) !important;
  color: #98989d;
}

:deep(.ios-table .n-data-table-td) {
  background-color: transparent !important;
  border-bottom: 1px solid rgba(0, 0, 0, 0.05) !important;
}

:deep(.dark) .ios-table .n-data-table-td {
  border-bottom: 1px solid rgba(255, 255, 255, 0.05) !important;
}

:deep(.n-data-table:not(.n-data-table--single-line) .n-data-table-td) {
  padding: 16px 12px;
}
</style>