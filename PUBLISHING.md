# 发布到 GitHub

此目录是粉团独立仓库的源码；旁边的 `release-assets` 放置发布附件，不要提交为普通源码文件。

1. 确认角色原图、改编图集和最终 ZIP 有权公开分享，并决定仓库的公开范围。没有确认之前，先设为 Private，不要发布 Release。
2. 在 GitHub Desktop 登录账户，通过 **File → Add local repository** 选择本目录，审阅文件，填写提交说明并点击 **Commit to main**。随后点击 **Publish repository**，把仓库名改为 `pink-pixel-pet`。确认素材公开使用权之前，保留 **Keep this code private**；确认后可取消勾选。GitHub Desktop 会创建远程仓库，不必预先在网页上建空仓库。不要随意选择开源许可证；代码和角色素材可以采用不同授权，需要先决定。
3. 上传前确认只包含 `core/`、`pink-pixel-desktop/` 的配置与图集、`docs/` 预览、构建脚本、README、PUBLISHING 和 `.gitignore`。历史源码备份、QA 中间文件与别的角色不应入库。若使用下方命令行方案，则先在 GitHub 网页上新建无初始化文件的空仓库。
4. 在仓库的 **Releases → Draft a new release** 创建 `v1.4.1`。添加附件 `release-assets/粉团桌宠-Windows-v1.4.1.zip` 和 `release-assets/SHA256.txt`，检查内容后发布。不要把 GitHub 自动生成的 Source code ZIP 当作安装包。
5. 后续共性修复先在桌宠工坊的 `core/` 完成并给各桌宠分别提高版本。用 `export_pink_github.py` 刷新此仓库源码，再运行自检、提交并新建对应版本的 Release。

PowerShell 命令（已安装 Git、完成 GitHub 身份验证且已经创建空远程仓库时）：

```powershell
git init
git add .
git status --short
git commit -m "Prepare PinkPixelPet v1.4.1"
git branch -M main
git remote add origin https://github.com/YOUR_ACCOUNT/pink-pixel-pet.git
git push -u origin main
```

把 `YOUR_ACCOUNT` 换成自己的 GitHub 用户名；远程仓库地址以 GitHub 页面显示的地址为准。上传前审阅 `git status` 和待提交文件。不要把令牌、账号信息或个人设置文件提交到仓库。
