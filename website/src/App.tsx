import {
  ArrowRight,
  Boxes,
  Check,
  ChevronRight,
  Command,
  Download,
  ExternalLink,
  Github,
  Keyboard,
  Layers3,
  Menu,
  Monitor,
  PackageCheck,
  Palette,
  ShieldCheck,
  Sparkles,
  TerminalSquare,
  X,
  Zap,
} from 'lucide-react'
import { useState } from 'react'
import appMain from '@/assets/app-main.png'
import terminalDemo from '@/assets/terminal-demo.png'
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/accordion'
import { Button } from '@/components/ui/button'
import {
  acceleratedDownloadUrl,
  downloadChannels,
  faqs,
  features,
  site,
  type Feature,
} from '@/data/site'

const iconMap = {
  zap: Zap,
  terminal: TerminalSquare,
  palette: Palette,
  keyboard: Keyboard,
  shield: ShieldCheck,
  box: PackageCheck,
} satisfies Record<Feature['icon'], typeof Zap>

function Logo({ compact = false }: { compact?: boolean }) {
  return (
    <a href="#top" className="group flex items-center gap-2.5" aria-label="PAM 首页">
      <span className="relative grid h-9 w-9 place-items-center rounded-xl bg-gradient-to-br from-violet-500 to-cyan-500 shadow-lg shadow-violet-900/30">
        <TerminalSquare className="h-5 w-5 text-white" strokeWidth={2.2} />
      </span>
      {!compact && (
        <span className="text-lg font-semibold tracking-tight text-white">
          PAM<span className="ml-1.5 text-xs font-normal text-slate-500">终端管理器</span>
        </span>
      )}
    </a>
  )
}

function Header() {
  const [open, setOpen] = useState(false)

  const nav = [
    { label: '功能', href: '#features' },
    { label: '界面预览', href: '#preview' },
    { label: '下载', href: '#download' },
    { label: '常见问题', href: '#faq' },
  ]

  return (
    <header className="fixed inset-x-0 top-0 z-50 border-b border-white/[0.06] bg-[#08080d]/80 backdrop-blur-xl">
      <div className="mx-auto flex h-16 max-w-6xl items-center justify-between px-5">
        <Logo />

        <nav className="hidden items-center gap-7 md:flex" aria-label="主导航">
          {nav.map((item) => (
            <a
              key={item.href}
              href={item.href}
              className="text-sm text-slate-400 transition-colors hover:text-white"
            >
              {item.label}
            </a>
          ))}
        </nav>

        <div className="hidden items-center gap-2 md:flex">
          <Button variant="ghost" size="icon" asChild className="text-slate-400 hover:text-white">
            <a href={site.repo} target="_blank" rel="noreferrer" aria-label="GitHub 仓库">
              <Github className="h-[18px] w-[18px]" />
            </a>
          </Button>
          <Button asChild size="sm" className="bg-white text-slate-950 hover:bg-slate-200">
            <a href={acceleratedDownloadUrl} target="_blank" rel="noreferrer">
              <Download className="mr-1.5 h-4 w-4" />
              下载
            </a>
          </Button>
        </div>

        <button
          type="button"
          className="grid h-9 w-9 place-items-center rounded-lg text-slate-300 md:hidden"
          onClick={() => setOpen(!open)}
          aria-label={open ? '关闭菜单' : '打开菜单'}
          aria-expanded={open}
        >
          {open ? <X className="h-5 w-5" /> : <Menu className="h-5 w-5" />}
        </button>
      </div>

      {open && (
        <nav className="border-t border-white/[0.06] bg-[#0b0b12] px-5 py-4 md:hidden" aria-label="移动端导航">
          <div className="flex flex-col gap-1">
            {nav.map((item) => (
              <a
                key={item.href}
                href={item.href}
                onClick={() => setOpen(false)}
                className="rounded-lg px-3 py-2.5 text-sm text-slate-300 hover:bg-white/5"
              >
                {item.label}
              </a>
            ))}
            <a
              href={site.repo}
              target="_blank"
              rel="noreferrer"
              className="rounded-lg px-3 py-2.5 text-sm text-slate-300 hover:bg-white/5"
            >
              GitHub 仓库
            </a>
          </div>
        </nav>
      )}
    </header>
  )
}

function Hero() {
  return (
    <section id="top" className="relative overflow-hidden pt-32 pb-20 md:pt-40 md:pb-28">
      <div className="pointer-events-none absolute inset-0 -z-10">
        <div className="absolute left-1/2 top-[-280px] h-[620px] w-[900px] -translate-x-1/2 rounded-full bg-violet-700/20 blur-[130px]" />
        <div className="absolute right-[-180px] top-40 h-[420px] w-[420px] rounded-full bg-cyan-600/10 blur-[120px]" />
        <div className="absolute inset-0 dot-grid opacity-[0.35] [mask-image:linear-gradient(to_bottom,black,transparent_75%)]" />
      </div>

      <div className="mx-auto grid max-w-6xl items-center gap-14 px-5 lg:grid-cols-[1fr_1.05fr] lg:gap-10">
        <div className="animate-fade-up">
          <div className="mb-6 inline-flex items-center gap-2 rounded-full border border-white/10 bg-white/[0.04] px-3 py-1.5 text-xs text-slate-300">
            <span className="h-1.5 w-1.5 rounded-full bg-emerald-400 shadow-[0_0_10px_#34d399]" />
            开源免费 · 完全本地运行 · v{site.version}
          </div>

          <h1 className="text-[2.6rem] font-bold leading-[1.15] tracking-tight text-white sm:text-5xl lg:text-[3.4rem]">
            把终端，调成
            <br />
            <span className="text-gradient">你顺手的样子</span>
          </h1>

          <p className="mt-6 max-w-xl text-base leading-7 text-slate-400 sm:text-lg">
            PAM 是一款 Windows 图形化终端管理器。别名、自定义命令、Starship 美化、全局热键，都在一个界面里搞定——不用再手写{' '}
            <code className="rounded bg-white/[0.07] px-1.5 py-0.5 font-mono text-[0.85em] text-slate-200">
              $PROFILE
            </code>
            。
          </p>

          <div className="mt-9 flex flex-wrap items-center gap-3">
            <Button
              asChild
              size="lg"
              className="h-12 rounded-xl bg-gradient-to-r from-violet-600 to-indigo-600 px-6 text-sm font-medium text-white shadow-lg shadow-violet-900/40 transition-all hover:from-violet-500 hover:to-indigo-500 hover:shadow-violet-900/60"
            >
              <a href={acceleratedDownloadUrl} target="_blank" rel="noreferrer">
                <Download className="mr-2 h-[18px] w-[18px]" />
                免费下载 Windows 版
              </a>
            </Button>
            <Button
              asChild
              variant="outline"
              size="lg"
              className="h-12 rounded-xl border-white/15 bg-white/[0.03] px-6 text-sm text-slate-200 hover:bg-white/[0.08] hover:text-white"
            >
              <a href={site.repo} target="_blank" rel="noreferrer">
                <Github className="mr-2 h-[18px] w-[18px]" />
                查看源码
                <ExternalLink className="ml-2 h-3.5 w-3.5 text-slate-500" />
              </a>
            </Button>
          </div>

          <div className="mt-7 flex flex-wrap gap-x-5 gap-y-2 text-xs text-slate-500">
            {['内置 .NET 运行时', '无需管理员权限', '支持 PS 5.1 与 7', '卸载干净'].map((item) => (
              <span key={item} className="flex items-center gap-1.5">
                <Check className="h-3.5 w-3.5 text-emerald-500" />
                {item}
              </span>
            ))}
          </div>
        </div>

        <div className="relative animate-fade-up delay-200">
          <div className="absolute -inset-8 -z-10 rounded-[2rem] bg-gradient-to-br from-violet-600/25 via-fuchsia-600/10 to-cyan-500/20 blur-3xl" />
          <div className="overflow-hidden rounded-2xl border border-white/[0.12] bg-[#12121a] shadow-2xl shadow-black/60">
            <div className="flex items-center gap-1.5 border-b border-white/[0.08] bg-white/[0.03] px-4 py-3">
              <span className="h-2.5 w-2.5 rounded-full bg-[#ff5f57]" />
              <span className="h-2.5 w-2.5 rounded-full bg-[#febc2e]" />
              <span className="h-2.5 w-2.5 rounded-full bg-[#28c840]" />
              <span className="ml-3 text-xs text-slate-500">PowerShell 管理器 · 别名启动</span>
            </div>
            <img
              src={appMain}
              alt="PAM 主界面的别名启动页"
              className="block w-full object-cover object-top"
            />
          </div>

          <div className="animate-float absolute -bottom-7 -left-4 hidden rounded-xl border border-white/10 bg-[#15151f]/95 px-4 py-3 shadow-xl shadow-black/50 backdrop-blur sm:block">
            <div className="flex items-center gap-2 text-xs text-slate-400">
              <span className="font-mono text-emerald-400">$</span>
              <span className="font-mono text-slate-200">blbl</span>
              <ChevronRight className="h-3 w-3 text-slate-600" />
              <span className="text-slate-400">启动程序</span>
            </div>
          </div>
        </div>
      </div>
    </section>
  )
}

function Stats() {
  const stats = [
    { value: '100%', label: '本地运行，不上传数据' },
    { value: '2 份', label: 'PROFILE 自动维护' },
    { value: '6 个', label: 'Starship 内置预设' },
    { value: '一个', label: '热键唤起同一个终端' },
  ]

  return (
    <section className="border-y border-white/[0.06] bg-white/[0.015]">
      <div className="mx-auto grid max-w-6xl grid-cols-2 gap-y-8 px-5 py-10 md:grid-cols-4">
        {stats.map((item) => (
          <div key={item.label} className="text-center md:border-r md:border-white/[0.07] md:last:border-0">
            <div className="text-2xl font-semibold text-white sm:text-3xl">{item.value}</div>
            <div className="mt-1.5 text-xs text-slate-500 sm:text-sm">{item.label}</div>
          </div>
        ))}
      </div>
    </section>
  )
}

function FeatureCard({ feature }: { feature: Feature }) {
  const Icon = iconMap[feature.icon]
  return (
    <div className="glass-card group relative overflow-hidden p-6 transition-colors duration-300 hover:border-white/20">
      <div
        className={`pointer-events-none absolute -right-10 -top-10 h-32 w-32 rounded-full bg-gradient-to-br ${feature.accent} blur-2xl opacity-0 transition-opacity duration-300 group-hover:opacity-100`}
      />
      <div className="relative mb-5 grid h-11 w-11 place-items-center rounded-xl border border-white/10 bg-white/[0.05]">
        <Icon className="h-5 w-5 text-slate-200" strokeWidth={1.8} />
      </div>
      <h3 className="relative text-base font-semibold text-white">{feature.title}</h3>
      <p className="relative mt-2.5 text-sm leading-6 text-slate-400">{feature.desc}</p>
    </div>
  )
}

function Features() {
  return (
    <section id="features" className="scroll-mt-20 py-24 md:py-28">
      <div className="mx-auto max-w-6xl px-5">
        <div className="max-w-2xl">
          <div className="mb-4 flex items-center gap-2 text-xs font-medium uppercase tracking-[0.18em] text-violet-400">
            <Sparkles className="h-3.5 w-3.5" />
            核心功能
          </div>
          <h2 className="text-3xl font-semibold tracking-tight text-white sm:text-4xl">
            常用操作，不用再记命令
          </h2>
          <p className="mt-4 text-base leading-7 text-slate-400">
            把重复的事情交给界面。你只管起名字、挑主题，剩下的交给 PAM 写进终端配置。
          </p>
        </div>

        <div className="mt-12 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {features.map((feature) => (
            <FeatureCard key={feature.title} feature={feature} />
          ))}
        </div>
      </div>
    </section>
  )
}

function Preview() {
  return (
    <section id="preview" className="scroll-mt-20 border-y border-white/[0.06] bg-white/[0.015] py-24 md:py-28">
      <div className="mx-auto grid max-w-6xl items-center gap-12 px-5 lg:grid-cols-2">
        <div className="relative order-2 lg:order-1">
          <div className="pointer-events-none absolute -inset-6 -z-10 rounded-3xl bg-cyan-600/10 blur-3xl" />
          <div className="overflow-hidden rounded-2xl border border-white/10 bg-[#0d0d14] shadow-2xl shadow-black/50">
            <div className="flex items-center justify-between border-b border-white/[0.08] px-4 py-3">
              <div className="flex items-center gap-2 text-xs text-slate-500">
                <TerminalSquare className="h-3.5 w-3.5" />
                Windows Terminal
              </div>
              <div className="flex gap-1.5">
                <span className="h-2 w-2 rounded-full bg-white/15" />
                <span className="h-2 w-2 rounded-full bg-white/15" />
                <span className="h-2 w-2 rounded-full bg-white/15" />
              </div>
            </div>
            <img src={terminalDemo} alt="终端中运行 PAM 命令的演示画面" className="block w-full" />
          </div>
          <p className="mt-3 text-center text-xs text-slate-600">终端效果演示画面</p>
        </div>

        <div className="order-1 lg:order-2">
          <div className="mb-4 flex items-center gap-2 text-xs font-medium uppercase tracking-[0.18em] text-cyan-400">
            <Command className="h-3.5 w-3.5" />
            终端里发生了什么
          </div>
          <h2 className="text-3xl font-semibold tracking-tight text-white sm:text-4xl">
            界面里配好，
            <br />
            终端里直接敲
          </h2>
          <p className="mt-4 text-base leading-7 text-slate-400">
            每条别名和命令都会生成标准的 PowerShell 函数，写在有明确边界的托管区里。你自己的配置不会被改写。
          </p>

          <div className="mt-7 space-y-3">
            {[
              { cmd: 'pls', desc: '列出全部别名及路径、备注' },
              { cmd: 'ongit <信息>', desc: '一条命令完成 add 与 commit' },
              { cmd: 'sudo <程序>', desc: '提权运行程序或别名' },
            ].map((item) => (
              <div key={item.cmd} className="flex items-center gap-4 rounded-xl border border-white/[0.07] bg-white/[0.025] px-4 py-3">
                <code className="min-w-[7.5rem] font-mono text-sm text-emerald-400">{item.cmd}</code>
                <span className="text-sm text-slate-400">{item.desc}</span>
              </div>
            ))}
          </div>

          <div className="mt-6 flex items-start gap-3 rounded-xl border border-amber-500/20 bg-amber-500/[0.06] px-4 py-3 text-sm text-amber-200/90">
            <Sparkles className="mt-0.5 h-4 w-4 shrink-0" />
            <span>改完配置后新开一个终端窗口即可生效，已开着的窗口不会自动刷新。</span>
          </div>
        </div>
      </div>
    </section>
  )
}

function Steps() {
  const steps = [
    { n: '01', title: '下载安装', desc: '下载安装包双击运行，不需要管理员权限，也不用预装 .NET。', icon: Download },
    { n: '02', title: '界面里配置', desc: '在「别名启动」加一条常用程序，或写一条自己的命令并保存。', icon: Layers3 },
    { n: '03', title: '新开终端试试', desc: '新开一个终端窗口，输入你刚起的名字；按热键随时唤起终端。', icon: TerminalSquare },
  ]

  return (
    <section className="py-24 md:py-28">
      <div className="mx-auto max-w-6xl px-5">
        <div className="text-center">
          <h2 className="text-3xl font-semibold tracking-tight text-white sm:text-4xl">三步，就能用起来</h2>
          <p className="mt-4 text-base text-slate-400">整个过程不到两分钟。</p>
        </div>

        <div className="relative mt-14 grid gap-5 md:grid-cols-3">
          <div className="absolute left-0 right-0 top-8 hidden h-px bg-gradient-to-r from-transparent via-white/10 to-transparent md:block" />
          {steps.map((step) => {
            const Icon = step.icon
            return (
              <div key={step.n} className="relative rounded-2xl border border-white/[0.07] bg-[#0d0d15] p-6">
                <div className="mb-6 flex items-center justify-between">
                  <span className="grid h-11 w-11 place-items-center rounded-xl bg-gradient-to-br from-violet-500/20 to-cyan-500/10 ring-1 ring-white/10">
                    <Icon className="h-5 w-5 text-violet-300" strokeWidth={1.8} />
                  </span>
                  <span className="font-mono text-2xl font-medium text-white/10">{step.n}</span>
                </div>
                <h3 className="text-lg font-medium text-white">{step.title}</h3>
                <p className="mt-2 text-sm leading-6 text-slate-400">{step.desc}</p>
              </div>
            )
          })}
        </div>
      </div>
    </section>
  )
}

function DownloadSection() {
  return (
    <section id="download" className="scroll-mt-20 border-t border-white/[0.06] bg-white/[0.015] py-24 md:py-28">
      <div className="mx-auto max-w-3xl px-5">
        <div className="text-center">
          <div className="mb-4 inline-flex items-center gap-2 rounded-full border border-violet-500/25 bg-violet-500/10 px-3 py-1.5 text-xs text-violet-300">
            <Boxes className="h-3.5 w-3.5" />
            最新版本 v{site.version}
          </div>
          <h2 className="text-3xl font-semibold tracking-tight text-white sm:text-4xl">现在就下载</h2>
          <p className="mt-4 text-base text-slate-400">选择适合你的下载方式。安装包内置运行时，双击即用。</p>
        </div>

        <div className="shadow-glow mt-10 overflow-hidden rounded-2xl border border-white/[0.1] bg-[#101018]">
          <div className="flex flex-wrap items-center justify-between gap-4 border-b border-white/[0.07] px-6 py-5">
            <div className="flex items-center gap-4">
              <span className="grid h-12 w-12 place-items-center rounded-xl bg-white/[0.06] ring-1 ring-white/10">
                <Monitor className="h-6 w-6 text-slate-200" strokeWidth={1.7} />
              </span>
              <div>
                <div className="font-medium text-white">PAM-Setup-{site.version}.exe</div>
                <div className="mt-1 text-xs text-slate-500">Windows 10 / 11 · 64 位 · 内置 .NET 10 运行时</div>
              </div>
            </div>
            <span className="rounded-full border border-emerald-500/25 bg-emerald-500/10 px-3 py-1 text-xs text-emerald-400">
              开源免费
            </span>
          </div>

          <div className="divide-y divide-white/[0.06]">
            {downloadChannels.map((channel) => {
              const ready = Boolean(channel.url)
              const content = (
                <>
                  <span className="grid h-10 w-10 shrink-0 place-items-center rounded-lg border border-white/[0.08] bg-white/[0.04]">
                    {channel.id === 'github' ? (
                      <Github className="h-[18px] w-[18px] text-slate-300" />
                    ) : (
                      <Download className="h-[18px] w-[18px] text-slate-300" />
                    )}
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="flex flex-wrap items-center gap-2">
                      <span className="text-sm font-medium text-white">{channel.name}</span>
                      {channel.badge && (
                        <span className="rounded border border-violet-500/25 bg-violet-500/10 px-1.5 py-0.5 text-[10px] text-violet-300">
                          {channel.badge}
                        </span>
                      )}
                    </span>
                    <span className="mt-1 block text-xs text-slate-500">{channel.desc}</span>
                  </span>
                  <span
                    className={`flex items-center gap-1.5 text-xs ${
                      ready ? 'text-violet-300' : 'text-slate-600'
                    }`}
                  >
                    {ready ? (
                      <>
                        下载 <ArrowRight className="h-3.5 w-3.5" />
                      </>
                    ) : (
                      '待配置'
                    )}
                  </span>
                </>
              )

              return ready ? (
                <a
                  key={channel.id}
                  href={channel.url}
                  target="_blank"
                  rel="noreferrer"
                  className="flex items-center gap-4 px-6 py-4 transition-colors hover:bg-white/[0.04]"
                >
                  {content}
                </a>
              ) : (
                <div key={channel.id} className="flex cursor-not-allowed items-center gap-4 px-6 py-4 opacity-60">
                  {content}
                </div>
              )
            })}
          </div>
        </div>

        <div className="mt-6 grid gap-3 sm:grid-cols-2">
          <div className="rounded-xl border border-white/[0.07] bg-white/[0.02] p-4">
            <div className="flex items-center gap-2 text-sm text-slate-200">
              <ShieldCheck className="h-4 w-4 text-emerald-400" />
              安装提示
            </div>
            <p className="mt-2 text-xs leading-5 text-slate-500">
              首次运行若出现 SmartScreen 提示，选择「更多信息 → 仍要运行」即可。
            </p>
          </div>
          <div className="rounded-xl border border-white/[0.07] bg-white/[0.02] p-4">
            <div className="flex items-center gap-2 text-sm text-slate-200">
              <Check className="h-4 w-4 text-emerald-400" />
              彻底卸载
            </div>
            <p className="mt-2 text-xs leading-5 text-slate-500">
              在「设置 → 应用」里卸载，会自动清理启动项与托管区，你的别名数据可保留。
            </p>
          </div>
        </div>
      </div>
    </section>
  )
}

function Faq() {
  return (
    <section id="faq" className="scroll-mt-20 py-24 md:py-28">
      <div className="mx-auto max-w-3xl px-5">
        <div className="text-center">
          <h2 className="text-3xl font-semibold tracking-tight text-white sm:text-4xl">常见问题</h2>
          <p className="mt-4 text-base text-slate-400">还有别的疑问？欢迎到 GitHub 提 Issue。</p>
        </div>

        <Accordion type="single" collapsible className="mt-10 space-y-2">
          {faqs.map((faq, index) => (
            <AccordionItem
              key={faq.q}
              value={`faq-${index}`}
              className="rounded-xl border border-white/[0.07] bg-white/[0.02] px-5 data-[state=open]:border-white/15"
            >
              <AccordionTrigger className="py-4 text-left text-sm font-medium text-slate-200 hover:no-underline hover:text-white">
                {faq.q}
              </AccordionTrigger>
              <AccordionContent className="pb-4 text-sm leading-6 text-slate-400">{faq.a}</AccordionContent>
            </AccordionItem>
          ))}
        </Accordion>

        <div className="mt-8 text-center">
          <Button asChild variant="link" className="text-sm text-violet-300 hover:text-violet-200">
            <a href={site.issues} target="_blank" rel="noreferrer">
              到 GitHub 反馈问题
              <ExternalLink className="ml-1.5 h-3.5 w-3.5" />
            </a>
          </Button>
        </div>
      </div>
    </section>
  )
}

function Footer() {
  return (
    <footer className="border-t border-white/[0.06] py-10">
      <div className="mx-auto flex max-w-6xl flex-col items-center justify-between gap-5 px-5 sm:flex-row">
        <div className="flex items-center gap-3">
          <Logo compact />
          <span className="text-xs text-slate-600">开源免费 · MIT © 2026 white</span>
        </div>
        <div className="flex items-center gap-5 text-xs text-slate-500">
          <a href={site.repo} target="_blank" rel="noreferrer" className="transition-colors hover:text-white">
            GitHub
          </a>
          <a href={site.releases} target="_blank" rel="noreferrer" className="transition-colors hover:text-white">
            Releases
          </a>
          <a href={site.license} target="_blank" rel="noreferrer" className="transition-colors hover:text-white">
            许可证
          </a>
          <a href="#top" className="transition-colors hover:text-white">
            返回顶部
          </a>
        </div>
      </div>
    </footer>
  )
}

export default function App() {
  return (
    <div className="min-h-screen bg-[#08080d]">
      <Header />
      <main>
        <Hero />
        <Stats />
        <Features />
        <Preview />
        <Steps />
        <DownloadSection />
        <Faq />
      </main>
      <Footer />
    </div>
  )
}
