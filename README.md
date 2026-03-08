# 📁 Simple File Host

一个基于 ASP.NET Core Minimal API 的轻量级文件托管服务，支持文件上传、分页浏览、多格式链接生成和实时统计。已部署至云服务器，开箱即用。

[![.NET](https://img.shields.io/badge/.NET-8.0+-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

## ✨ 核心特性

- **流式上传处理** - 直接写入磁盘，不经过 temp 文件夹，避免大文件内存占用和磁盘碎片
- **智能格式生成** - 自动为图片生成 Markdown/HTML/响应式代码片段，一键复制即用
- **分页懒加载** - 支持自定义每页数量（8/12/24/48），适配不同带宽环境
- **白名单安全** - 严格的扩展名过滤，禁止可执行文件上传
- **多尺寸输出** - 图片支持小(300px)/中(500px)/大(800px)/响应式 四种 HTML 嵌入尺寸

## 🚀 快速开始

```bash
# 克隆仓库
git clone https://github.com/yourusername/FileHost.git
cd FileHost
```

# 运行（自动创建 uploads 目录）
dotnet run

# 访问 http://localhost:5000

## 📡 API端点

| 端点                              | 方法     | 说明                         |
| :------------------------------ | :----- | :------------------------- |
| `GET /api/hello`                | -      | 服务健康检查                     |
| `GET /api/stats`                | -      | 文件统计（数量/总大小）               |
| `GET /api/list`                 | GET    | 分页列表 `?page=1&pageSize=12` |
| `POST /api/upload`              | POST   | 多文件上传（multipart/form-data） |
| `DELETE /api/delete/{fileName}` | DELETE | 删除指定文件                     |
| `/files/{fileName}`             | GET    | 静态文件访问（带缓存头）               |

## 🛡️ 安全配置
```csharp
// 允许的文件类型
.jpg .jpeg .png .gif .webp .bmp .svg    // 图片
.pdf .txt .doc .docx .xls .xlsx        // 文档
.zip .rar .7z                           // 压缩包
.mp3 .mp4 .wav                         // 音视频

// 上传限制
单文件最大: 100MB
请求体限制: 100MB
```

## 🌐 生产部署
环境要求
部分网站会要求 HTTPS（部分网站图片显示必需）

## 🖼️ 使用场景
图床服务 - 为 Markdown 文档、博客提供稳定图片外链
临时文件中转 - 团队内部文件快速共享
静态资源托管 - 小型项目的附件存储方案

## 开源协议
```plain
MIT License

Copyright (c) [2026-3-8] [lxy1234cn]

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
