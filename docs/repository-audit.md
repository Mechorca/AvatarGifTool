# 仓库地址与密钥核对

核对日期：2026-11-16

## GitHub 地址

当前仓库远程地址：

```text
git@github.com:Mechorca/AvatarGifTool.git
```

`git remote -v` 的 fetch / push 均指向该地址。

文档中的地址核对：

- `README.md` 的 Releases 链接：`https://github.com/Mechorca/AvatarGifTool/releases`，与远程仓库一致。
- `README.md` 的上游项目链接：`https://github.com/Jancy-49/WzComparerR2-CMS`，用于标注改造来源，属于预期地址。
- `.github/workflows/release.yml` 使用当前 GitHub 仓库上下文，没有硬编码其他仓库地址。
- `WzComparerR2/FrmUpdater.cs` 仍指向上游 `Jancy-49/WzComparerR2-CMS`。该文件属于上游 WzComparerR2 主程序，AvatarGifTool 当前不使用该更新器；为避免误改上游更新流程，本次未修改。

## 令牌与密钥

核对结果：

- `.github/workflows/release.yml` 只使用 GitHub Actions 内置的 `secrets.GITHUB_TOKEN`，没有硬编码个人访问令牌。
- 仓库已跟踪文件中未发现 `.key`、`.pem`、`.pfx`、`.snk`、`.cer`、`.crt`、`.p12`、`.jks`、`.keystore` 等密钥或证书文件。
- 仓库已跟踪文件中未发现 `.env` 文件。
- `AvatarGifTool/build.env` 存在于本地工作区，但已被 `.gitignore` 忽略，不会提交；内容仅包含本地 `.NET` 路径和构建参数，不包含密钥。
- `AvatarGifTool/build.env.example` 是公开示例，不含敏感信息。
- `azure-pipelines.yml` 属于上游 CI 配置，包含 Azure 服务连接名、订阅 ID、存储账户 `wcr2publish`、容器 `ci-build` 和 GitHub 服务连接名 `Kagamia` 等资源/连接标识，不是明文密钥。
- `azure-pipelines.yml` 在上传制品时通过 `az storage account keys list` 在流水线运行时读取存储账户密钥，再生成 SAS URL；仓库中没有提交该密钥本身。若此仓库不使用上游 Azure 流水线，建议后续单独清理或替换该文件。

## 备注

本次只做只读核对，没有修改远程地址或工作流密钥配置。若以后将 `WzComparerR2` 更新器作为发布程序的一部分，需要重新核对 `FrmUpdater.cs` 的仓库地址和发布资源命名。
