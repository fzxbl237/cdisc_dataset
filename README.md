# PatChes

基于 Avalonia 的 CDISC 数据集与 Define-XML 管理桌面应用，面向 SDTM/ADaM 数据定义维护、标准数据导入、Define 元数据编辑、Define-XML 导出与验证。

## 项目状态

项目目前处于持续开发阶段，主要功能已经接入，但部分规则和工作流仍在完善中。当前 Define 验证器是基于项目内独立 `Validator.Define` 模块实现的第一阶段版本，不依赖 Pinnacle 21 Validator 运行时。

## 主要功能

- 项目管理与 SDTM/ADaM 数据类型切换
- Define 元数据维护
  - Datasets
  - Variables
  - Value Levels
  - Code Lists
  - Terms
  - Comments
  - Methods
  - Documents
  - Dictionaries
  - Where Clauses
- SDTM XPT 文件导入
  - 读取 SAS Transport 文件
  - 自动生成 Dataset 和 Variable 元数据
  - 根据数据内容推导变量数据类型、长度和小数位
  - 生成 SUPPQUAL Value Level
  - 生成 TS Value Level
  - TSVAL Value Level 按当前 `TSPARMCD` 对应的实际值推导数据类型
- 标准元数据与受控术语关联
- Code List、Term、Variable、Method、Dictionary 等对象的引用管理
- 删除引用确认
  - 仅删除模型，保留引用
  - 删除模型并清空引用
- Define-XML 导出
  - Define-XML 2.1
  - ODM 1.3.2
  - XLink 引用
  - Value List 和 Where Clause
- Define-XML 验证
  - XML 格式与声明检查
  - ODM/Define-XML Schema 校验
  - Define 结构和引用完整性检查
  - 规则诊断信息写入 Issues 数据表
  - 在 Issues 页面查看验证结果
- Excel 导出
- 数据校验与重复项检测
- 支持搜索、错误筛选、批量删除和编辑

## 技术栈

- .NET 10
- C#
- Avalonia UI 12
- AtomUI Desktop Controls
- AsyncNavigation.Avalonia
- CommunityToolkit.Mvvm
- ReactiveUI.Avalonia
- DynamicData
- FluentValidation
- SqlSugar
- SQLite
- LiteDB
- ClosedXML
- MiniExcel
- Mapster
- Define-XML 2.1 XSD

## 解决方案结构

```text
PatChes.sln
├── PatChes/       主 Avalonia 桌面应用
├── Validator.Api/       数据校验相关 API 和模型
├── Validator.Core/      通用校验核心逻辑
├── Validator.Data/      校验数据访问与规则数据
├── Validator.Define/    独立 Define-XML 验证器
└── WhereClause/         Where Clause 解析与处理模块
```

### 主应用目录

```text
PatChes/
├── Assets/
│   └── DefineXmlSchema/       ODM 1.3.2 和 Define-XML 2.1 Schema
├── Controls/                  自定义控件和 DataGrid 扩展
├── Models/                    数据库实体和 DTO
├── Services/                  数据访问、导入、导出、验证和业务服务
├── Validations/               FluentValidation 校验器
├── ViewModels/                MVVM ViewModel
├── Views/                     Avalonia 页面和对话框
├── App.axaml.cs               DI、数据库和导航注册
└── PatChes.csproj       主应用项目文件
```

## 环境要求

- Windows
- .NET SDK 10.0 或更高版本
- .NET SDK 9.0 或更高版本，用于构建 `Validator.Api`、`Validator.Core`、`Validator.Data` 和 `Validator.Define`
- 可访问 NuGet 源
- Rider、Visual Studio 或其他支持 Avalonia 的 IDE

## 构建

在仓库根目录执行：

```powershell
dotnet restore PatChes.sln
dotnet build PatChes.sln
```

当前解决方案的主应用目标框架为 `net10.0`，Define 验证相关子项目目标框架为 `net9.0`。

## 运行

```powershell
dotnet run --project PatChes/PatChes.csproj
```

也可以直接使用 Rider 或 Visual Studio 打开：

```text
PatChes.sln
```

## 本地数据库

应用启动时会自动初始化或升级 SQLite 数据库：

- `PatChes.db`：项目数据、Define 元数据和 Issues
- `cdisc_setting.db`：标准变量、标准数据集、受控术语和其他设置数据
- `cdisc_files.db`：LiteDB 文件存储，用于保存上传的 XPT、PDF 等项目文件

这些数据库属于本地运行数据，不建议提交到版本库。首次运行时，应用会根据当前 Code First 配置创建相关表，并执行部分兼容性字段修复。

## 基本使用流程

1. 启动应用并创建或选择项目。
2. 在项目中选择 SDTM 或 ADaM 数据类型。
3. 在 Files 页面上传项目文件。
4. 对 SDTM XPT 文件执行标准数据加载。
5. 在 Datasets、Variables、Value Levels 等页面检查和调整元数据。
6. 补充 Code Lists、Terms、Comments、Methods、Documents 和 Dictionaries。
7. 在 Define 页面查看和编辑 Define 元数据。
8. 导出 Define-XML 或 Excel 文件。
9. 在 Issues 页面点击 Define-XML 验证按钮。
10. 根据验证结果修复错误和警告后重新验证。

## Define-XML 验证

Define-XML 验证由 `Validator.Define` 提供：

```csharp
public interface IDefineValidator
{
    DefineValidationResult Validate(
        string xml,
        DefineValidationOptions options);
}
```

验证过程包括：

1. 由 `DefineXmlExportService` 生成当前项目 Define-XML。
2. 同时加载 ODM 1.3.2 和 Define-XML 2.1 Schema。
3. 执行 XML 声明、命名空间、结构、引用完整性和内容规则检查。
4. 将诊断结果写入 `Issue` 表。
5. 以 `EntityType = "Define"`、`EntityId = 0` 归属于当前项目和当前数据类型。
6. 在 Issues DataGrid 中显示验证结果。

当前验证规则已覆盖 XML 基础结构、OID/ID 重复、Dataset、Variable、ItemRef、CodeList、Method、Comment、ValueList、Document、Origin、TranslatedText、标准组合等核心检查。完整迁移全部外部规则仍在持续进行中。

## 外部 DLL 依赖

主项目文件中目前包含 ProDataGrid 相关的本地 DLL 引用：

```text
..\..\..\Desktop\prodatagrid_dll\avalonia12\Avalonia.Controls.DataGrid.dll
..\..\..\Desktop\prodatagrid_dll\avalonia12\ProDataGrid.FormulaEngine.dll
..\..\..\Desktop\prodatagrid_dll\avalonia12\ProDataGrid.FormulaEngine.Excel.dll
```

这些路径是当前开发环境中的本地路径。若在其他机器构建，需要：

1. 准备对应版本的 DLL；
2. 修改 `PatChes/PatChes.csproj` 中的 `HintPath`；或
3. 将相关依赖改为 NuGet 或项目引用。

## 数据导入说明

### SDTM XPT

SDTM XPT 导入逻辑位于 `PatChes/ViewModels/FileViewModel.cs`，主要步骤包括：

- 解析 SAS Transport 文件；
- 读取变量名、标签、格式、长度和记录值；
- 推导变量数据类型；
- 根据标准元数据补充变量角色和必填属性；
- 生成 Code List 和 Terms；
- 对 SUPPQUAL 数据集生成 QVAL Value Level；
- 对 TS 数据集按 `TSPARMCD` 生成 TSVAL Value Level。

TSVAL 是混合类型列，因此 Value Level 的类型应根据同一 `TSPARMCD` 下的实际值推导，而不是直接使用 TSVAL 物理列的整体类型。

## 删除引用策略

Code Lists、Methods、Comments、Dictionaries 和 Documents 的单项右键删除会先显示引用位置，并提供两种操作：

- **仅删除模型，保留引用**：删除被引用的模型对象，但不修改其他实体中的引用字段。
- **删除模型并清空引用**：删除模型对象，同时清理相关 Dataset、Variable、Value Level、Term 等实体中的引用。

共享确认组件为：

- `DeleteConfirmedDialog`
- `DeleteConfirmedViewModel`

## 开发约定

- 主应用使用 MVVM 架构。
- 页面导航和对话框通过 Microsoft.Extensions.DependencyInjection 注册。
- 数据访问统一通过 SqlSugar 服务层完成。
- UI 校验主要使用 FluentValidation。
- 新增功能应优先复用现有 Service、ViewModel 和 Dialog 模式。
- 不要提交本地数据库、构建产物、IDE 配置和包含敏感信息的配置文件。

## 验证命令

构建解决方案：

```powershell
dotnet build PatChes.sln --no-restore
```

检查 Git 差异格式：

```powershell
git diff --check
```

当前仓库未配置完整的自动化单元测试套件，功能验证主要依靠构建检查、实际项目数据导入、Define-XML 导出和桌面 UI 操作验证。

## 已知限制

- Define 验证规则尚未完全覆盖用户提供源码中的所有规则。
- SDTM/ADaM 标准变量交叉校验、术语交叉校验和 ARM 分析结果规则仍在持续完善。
- 当前主项目依赖本机 ProDataGrid DLL 路径，跨机器构建需要调整引用。
- 数据库为本地 SQLite/LiteDB 文件，当前没有独立数据库迁移工具。
- 当前未提供正式安装包、自动更新机制或 CI/CD 发布配置。

## 许可

当前仓库未声明正式开源许可证。未经项目维护者确认，不应将本项目或其中的规则数据作为公开发行组件使用。
