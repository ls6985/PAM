export const site = {
  name: 'PAM',
  tagline: 'Windows 终端管理器',
  version: '1.0.0',
  repo: 'https://github.com/ls6985/PAM',
  releases: 'https://github.com/ls6985/PAM/releases',
  releaseLatest: 'https://github.com/ls6985/PAM/releases/latest',
  issues: 'https://github.com/ls6985/PAM/issues',
  license: 'https://github.com/ls6985/PAM/blob/main/LICENSE',
}

/** 国内加速下载直链。页面顶部按钮与下载区共用这一处配置。 */
export const acceleratedDownloadUrl =
  'https://v4.gh-proxy.org/https://github.com/ls6985/PAM/releases/download/v1.0.0/PAM-Setup-1.0.0.exe'

/** 下载渠道集中配置。 */
export const downloadChannels = [
  {
    id: 'github',
    name: '下载安装包（GitHub 直连）',
    desc: '官方发布页，始终为最新版本',
    badge: '官方',
    url: site.releaseLatest,
    primary: true,
  },
  {
    id: 'proxy',
    name: '国内加速下载',
    desc: '通过 gh-proxy 加速，国内网络更快',
    badge: '',
    url: acceleratedDownloadUrl,
    primary: false,
  },
] as const

export type Feature = {
  icon: 'zap' | 'terminal' | 'palette' | 'keyboard' | 'shield' | 'box'
  title: string
  desc: string
  accent: string
}

export const features: Feature[] = [
  {
    icon: 'zap',
    title: '别名启动',
    desc: '在界面里给常用程序起个短名字，终端输入即启动，还能固定附带启动参数。',
    accent: 'from-violet-500/20 to-violet-500/0',
  },
  {
    icon: 'terminal',
    title: '自定义命令',
    desc: '把常用操作写成一条命令，支持传参、随时启停，内置 pls / ongit / sudo。',
    accent: 'from-cyan-500/20 to-cyan-500/0',
  },
  {
    icon: 'palette',
    title: '终端美化',
    desc: '六个 Starship 预设、字体安装、Windows Terminal 外观，改前自动备份。',
    accent: 'from-fuchsia-500/20 to-fuchsia-500/0',
  },
  {
    icon: 'keyboard',
    title: '全局热键',
    desc: '默认 Ctrl+Alt+T 唤起同一个终端窗口，不重复开窗口，会话与目录都保留。',
    accent: 'from-amber-500/20 to-amber-500/0',
  },
  {
    icon: 'shield',
    title: '本地运行',
    desc: '不收集任何数据，不上传你的别名、命令或路径。开源代码可自查。',
    accent: 'from-emerald-500/20 to-emerald-500/0',
  },
  {
    icon: 'box',
    title: '开箱即用',
    desc: '安装包内置 .NET 运行时，无需管理员权限，卸载也干净。',
    accent: 'from-blue-500/20 to-blue-500/0',
  },
]

export const faqs = [
  {
    q: '安装需要管理员权限吗？要另外装 .NET 吗？',
    a: '都不需要。安装包已内置 .NET 10 桌面运行时，双击安装即可，默认装到 %LOCALAPPDATA%\\Programs\\PAM。',
  },
  {
    q: '为什么改完设置，终端里没反应？',
    a: '$PROFILE 只在终端启动时读取一次。改完别名、命令或美化后，新开一个终端窗口即可生效；运行时开关（如友好提示）会即时生效。',
  },
  {
    q: '会覆盖我自己写的 $PROFILE 吗？',
    a: '不会。PAM 只接管标记之间的一段托管区，标记之外你自己写的内容逐行原样保留；已有同名 alias / function 时包装函数会让位。',
  },
  {
    q: '支持 Windows PowerShell 5.1 吗？',
    a: '支持。同一份配置会分别写入 PowerShell 7 与 Windows PowerShell 5.1 的 $PROFILE，安装任一端都能使用。',
  },
  {
    q: '下载后 Windows 提示 SmartScreen 怎么办？',
    a: '免费开源软件未做代码签名，首次运行可能出现提示。选择「更多信息 → 仍要运行」即可；确认文件来自本项目 Releases 或本页下载渠道。',
  },
  {
    q: '卸载后会留下什么？',
    a: '卸载会结束运行中的实例、移除登录启动项，并清理两份 $PROFILE 里的 PAM 托管区（原文件自动备份）。别名与设置可选择保留在 %AppData%\\PAM。',
  },
]
