# PAM 官网

PAM 的官网源码，部署在 GitHub Pages：<https://ls6985.github.io/PAM/>

技术栈：React + TypeScript + Vite + Tailwind CSS + shadcn/ui。

## 本地开发

```bash
npm install
npm run dev
```

## 构建

```bash
npm run build
```

产物在 `dist/`。`vite.config.ts` 设置了 `base: './'`，资源使用相对路径，适合部署在 Pages 的子路径下。

## 发布

`gh-pages` 分支保存构建产物，Pages 的 source 指向该分支根目录。发布时把 `dist/` 的内容同步到 `gh-pages` 分支并推送，保留根目录的 `.nojekyll`。

下载链接集中在 `src/data/site.ts`：`acceleratedDownloadUrl` 是国内加速直链，页面三个下载入口共用这一处配置。发布新版本时更新该文件即可。
