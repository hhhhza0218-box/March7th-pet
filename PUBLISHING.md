# 维护与发布

粉团桌宠的 GitHub 仓库已公开。请在新增素材或发布文件前核对来源与使用权；收到权利投诉时，通过仓库 Issues 跟进并核实，必要时删除相关内容。

本仓库是粉团独立软件的源码快照，安装包放在 GitHub Releases。历史版本的 ZIP 保留原始内容，逐版说明在 `release-notes/` 和 `CHANGELOG.md`。早期版本没有完整的逐版源码快照，不能把 GitHub 自动生成的历史标签 Source code ZIP 当作精确旧版源码。

以后修复共性问题时，先在 `D:/codex/pets/desktop-pet-workshop/core/` 修改并给受影响的桌宠分别提高版本。使用 `export_pink_github.py` 刷新本仓库中的粉团源码和素材，运行 `build.ps1` 自检，再提交并创建对应版本的 Release。发布附件应使用经过校验的独立安装包和 SHA256 文件。不要把 QA 中间文件、其他角色或个人设置提交到本仓库。
