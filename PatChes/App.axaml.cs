using System;
using System.Collections.Generic;
using AtomUI;
using AtomUI.Controls;
using AtomUI.Desktop.Controls;
using AtomUI.Theme;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PatChes.Models;
using PatChes.Models.Dto;
using PatChes.Models.Enums;
using PatChes.Navigation;
using PatChes.Models.Settings;
using PatChes.Services;
using PatChes.Services.Interface;
using PatChes.Validations;
using PatChes.Validations.Form;
using PatChes.ViewModels;
using PatChes.ViewModels.Defines;
using PatChes.ViewModels.Dialogs;
using PatChes.Views;
using PatChes.Views.Defines;
using PatChes.Views.Dialogs;
using FluentValidation;
using LiteDB;
using Mapster;
using MapsterMapper;
using AsyncNavigation;
using AsyncNavigation.Abstractions;
using AsyncNavigation.Avalonia;
using AtomUI.Localization;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using Validator.Define;
using DbType = System.Data.DbType;
using VariableCodeList = PatChes.Models.Settings.VariableCodeList;
using Window = AtomUI.Desktop.Controls.Window;

namespace PatChes;

public partial class App : Application
{
    public override void Initialize()
    {
        base.Initialize();
        AvaloniaXamlLoader.Load(this);
        this.UseAtomUI(builder =>
        {
            builder.UseLanguages(LanguageTags.ZhCN, [LanguageTags.En,LanguageTags.ZhCN]);
            builder.WithInitialTheme(IThemeManager.DEFAULT_THEME_ID);
            builder.UseAlibabaSansFont();
            builder.UseDesktopControls();
            builder.UseDesktopDataGrid();
        });
    }

    private static void RegisterDialog<TView, TViewModel>(IServiceCollection services, string name)
        where TView : Avalonia.Controls.Control
        where TViewModel : class
    {
        services.AddTransient<TView>();
        services.AddTransient<TViewModel>();
        services.AddKeyedTransient<Avalonia.Controls.Control>(name, (serviceProvider, _) =>
        {
            var view = serviceProvider.GetRequiredService<TView>();
            view.DataContext = serviceProvider.GetRequiredService<TViewModel>();
            return view;
        });
    }

    private static string ResolveFilesDatabasePath()
    {
        var baseDirectoryPath = System.IO.Path.Combine(AppContext.BaseDirectory, "cdisc_files.db");
        if (HasProjectFiles(baseDirectoryPath))
            return baseDirectoryPath;

        var workingDirectoryPath = System.IO.Path.Combine(Environment.CurrentDirectory, "cdisc_files.db");
        if (!string.Equals(baseDirectoryPath, workingDirectoryPath, StringComparison.OrdinalIgnoreCase) &&
            HasProjectFiles(workingDirectoryPath))
        {
            return workingDirectoryPath;
        }

        return baseDirectoryPath;
    }

    private static bool HasProjectFiles(string databasePath)
    {
        if (!System.IO.File.Exists(databasePath))
            return false;

        try
        {
            using var database = new LiteDatabase($"Filename={databasePath};Connection=shared");
            return database.GetCollection<ProjectFile>("project_files").Count() > 0;
        }
        catch
        {
            return false;
        }
    }

    private static void FixHasErrorsDefault(ISqlSugarClient sqlSugar)
    {
        sqlSugar.Ado.ExecuteCommand("UPDATE Project SET HasErrors = COALESCE(HasErrors, 0)");
        sqlSugar.Ado.ExecuteCommand("UPDATE Document SET HasErrors = COALESCE(HasErrors, 0)");
        EnsureDocumentDuplicateColumns(sqlSugar);
        sqlSugar.Ado.ExecuteCommand("UPDATE Dataset SET HasErrors = COALESCE(HasErrors, 0)");
        sqlSugar.Ado.ExecuteCommand("UPDATE Variable SET HasErrors = COALESCE(HasErrors, 0)");
        sqlSugar.Ado.ExecuteCommand("UPDATE Variable SET CdiscDataType = COALESCE(CdiscDataType, 0)");
        sqlSugar.Ado.ExecuteCommand("UPDATE Variable SET HasNoData = COALESCE(HasNoData, 'No')");
        sqlSugar.Ado.ExecuteCommand("UPDATE CodeList SET HasErrors = COALESCE(HasErrors, 0)");
        sqlSugar.Ado.ExecuteCommand("UPDATE Term SET HasErrors = COALESCE(HasErrors, 0)");
        sqlSugar.Ado.ExecuteCommand("UPDATE Comment SET HasErrors = COALESCE(HasErrors, 0)");
        sqlSugar.Ado.ExecuteCommand("UPDATE Method SET HasErrors = COALESCE(HasErrors, 0)");
        sqlSugar.Ado.ExecuteCommand("UPDATE Method SET HasUniqueIdDuplicate = COALESCE(HasUniqueIdDuplicate, 0)");
        sqlSugar.Ado.ExecuteCommand("UPDATE Method SET HasNameDuplicate = COALESCE(HasNameDuplicate, 0)");
        sqlSugar.Ado.ExecuteCommand("UPDATE ValueLevel SET HasErrors = COALESCE(HasErrors, 0)");
        sqlSugar.Ado.ExecuteCommand("UPDATE Dictionary SET HasErrors = COALESCE(HasErrors, 0)");
        sqlSugar.Ado.ExecuteCommand("UPDATE Term SET IsNameDuplicate = COALESCE(IsNameDuplicate, 0)");
        sqlSugar.Ado.ExecuteCommand("UPDATE Term SET DecodedValueConsistent = COALESCE(DecodedValueConsistent, 1)");
        sqlSugar.Ado.ExecuteCommand("UPDATE Issue SET Severity = Severity");
    }

    private static void EnsureDocumentDuplicateColumns(ISqlSugarClient sqlSugar)
    {
        var maintenance = sqlSugar.DbMaintenance;
        if (!maintenance.IsAnyColumn("Document", "IsUniqueIdDuplicate", false))
            sqlSugar.Ado.ExecuteCommand("ALTER TABLE Document ADD COLUMN IsUniqueIdDuplicate INTEGER NOT NULL DEFAULT 0");
        if (!maintenance.IsAnyColumn("Document", "IsTitleDuplicate", false))
            sqlSugar.Ado.ExecuteCommand("ALTER TABLE Document ADD COLUMN IsTitleDuplicate INTEGER NOT NULL DEFAULT 0");
        if (!maintenance.IsAnyColumn("Document", "IsHrefDuplicate", false))
            sqlSugar.Ado.ExecuteCommand("ALTER TABLE Document ADD COLUMN IsHrefDuplicate INTEGER NOT NULL DEFAULT 0");
    }

    private static void SeedTemplateDocuments(ISqlSugarClient settingDb)
    {
        if (settingDb.Queryable<TemplateDocument>().Any())
            return;

        settingDb.Insertable(new List<TemplateDocument>
        {
            new()
            {
                UniqueId = "acrf",
                Title = "Annotated CRF",
                Href = "acrf.pdf",
                CdiscDataType = CdiscDataType.Sdtm
            },
            new()
            {
                UniqueId = "cSDRG",
                Title = "Study Data Reviewer's Guide",
                Href = "csdrg.pdf",
                CdiscDataType = CdiscDataType.Sdtm
            }
        }).ExecuteCommand();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();

        var services = new ServiceCollection();
        services.AddNavigationSupport(new NavigationOptions
            {
                LoadingIndicatorDelay = TimeSpan.Zero
            })
            .RegisterInnerIndicatorProvider<DefineNavigationIndicatorProvider>()
            .AddSingleton<MainWindowViewModel>()
            .RegisterNavigation<ProjectView, ProjectViewModel>("Projects")
            .RegisterNavigation<SdtmDefineView, SdtmDefineViewModel>("SdtmDefine")
            .RegisterNavigation<TerminologyView, TerminologyViewModel>("Terminology")
            .RegisterNavigation<FileView, FileViewModel>("Files")
            .RegisterNavigation<CommentsView, CommentsViewModel>("Comments")
            .RegisterNavigation<DocumentsView, DocumentsViewModel>("Documents")
            .RegisterNavigation<MethodsView, MethodsViewModel>("Methods")
            .RegisterNavigation<ValueLevelsView, ValueLevelsViewModel>("ValueLevels")
            .RegisterNavigation<CodeListView, CodeListViewModel>("CodeLists")
            .RegisterNavigation<TermView, TermViewModel>("Terms")
            .RegisterNavigation<VariablesView, VariablesViewModel>("Variables")
            .RegisterNavigation<DatasetsView, DatasetsViewModel>("Datasets")
            .RegisterNavigation<DictionariesView, DictionariesViewModel>("Dictionaries")
            .RegisterNavigation<IssueView, IssueViewModel>("Issues")
            .RegisterNavigation<DefineIssueView, DefineIssueViewModel>("Define Issues")
            .AddSingleton<IDialogHostService, DialogHostService>()
            .AddSingleton<PatChes.Services.IDialogService, PatChes.Services.DialogService>();

        RegisterDialog<ProjectDialog, EditProjectViewModel>(services, "ProjectDialog");
        RegisterDialog<ImportSettingDatasetsDialog, ImportSettingDatasetsViewModel>(services, "ImportSettingDatasetsDialog");
        RegisterDialog<ImportSettingDocumentsDialog, ImportSettingDocumentsViewModel>(services, "ImportSettingDocumentsDialog");
        RegisterDialog<ImportSettingVariablesDialog, ImportSettingVariablesViewModel>(services, "ImportSettingVariablesDialog");
        RegisterDialog<CommentDialog, CommentViewModel>(services, "CommentDialog");
        RegisterDialog<DocumentDialog, DocumentViewModel>(services, "DocumentDialog");
        RegisterDialog<DictionaryDialog, DictionaryViewModel>(services, "DictionaryDialog");
        RegisterDialog<MethodDialog, MethodViewModel>(services, "MethodDialog");
        RegisterDialog<AssignVariablesDialog, AssignVariablesViewModel>(services, "AssignVariablesDialog");
        RegisterDialog<AssignCommentVariablesDialog, AssignCommentVariablesViewModel>(services, "AssignCommentVariablesDialog");
        RegisterDialog<BuildValueLevelsDialog, BuildValueLevelsViewModel>(services, "BuildValueLevelsDialog");
        RegisterDialog<WhereClauseEditorDialog, WhereClauseEditorViewModel>(services, "WhereClauseEditorDialog");
        RegisterDialog<VariableDialog, VariableViewModel>(services, "VariableDialog");
        RegisterDialog<DeleteConfirmedDialog, DeleteConfirmedViewModel>(services, "DeleteConfirmedDialog");
        RegisterDialog<EditKeyVariablesDialog, EditKeyVariablesViewModel>(services, "EditKeyVariables");
        RegisterDialog<CodeListDialog, CodeListDialogViewModel>(services, "CodeListDialog");
        RegisterDialog<EditTermsDialog, EditTermsViewModel>(services, "EditTermsDialog");
        RegisterDialog<TermsDialog, TermsViewModel>(services, "TermsDialog");
        RegisterDialog<PairTermsDialog, PairTermsViewModel>(services, "PairTermsDialog");
        RegisterDialog<UnsavedChangesDialog, UnsavedChangesViewModel>(services, "UnsavedChangesDialog");
        RegisterDialog<ConfirmDialog, ConfirmViewModel>(services, "ConfirmDialog");
        RegisterDialog<MergeCodeListsDialog, MergeCodeListsViewModel>(services, "MergeCodeListsDialog");

        var config = new TypeAdapterConfig();
        config.NewConfig<Dataset, Dataset>();
        services.AddSingleton(config);
        services.AddSingleton<IMapper, Mapper>();
        services.AddSingleton<ISqlSugarClient>(_ =>
        {
            var sqlSugar = new SqlSugarClient([
                new ConnectionConfig { ConfigId = "project", DbType = SqlSugar.DbType.Sqlite, ConnectionString = "DataSource=PatChes.db", IsAutoCloseConnection = true, InitKeyType = InitKeyType.Attribute },
                new ConnectionConfig { ConfigId = "setting", DbType = SqlSugar.DbType.Sqlite, ConnectionString = "DataSource=cdisc_setting.db", IsAutoCloseConnection = true }
            ]);
            var sqlSugarProject = sqlSugar.GetConnection("project");
            sqlSugarProject.CodeFirst.InitTables<Project, Document, Dataset, Variable>();
            sqlSugarProject.CodeFirst.InitTables<CodeList, Term, Comment, Method, ValueLevel>();
            sqlSugarProject.CodeFirst.InitTables<Dictionary, Issue, DefineIssue, WhereClause, DictionaryVersion>();
            var sqlSugarSetting = sqlSugar.GetConnection("setting");
            sqlSugarSetting.CodeFirst.InitTables<VariableCodeList, CodeListTerm,
                CodeListReference, DatasetTemplate, VariableTemplate>();
            sqlSugarSetting.CodeFirst.InitTables<TemplateDocument>();
            SeedTemplateDocuments(sqlSugarSetting);
            FixHasErrorsDefault(sqlSugar);
            return sqlSugar;
        });
        var filesDatabasePath = ResolveFilesDatabasePath();
        services.AddSingleton<ILiteDatabase>(_ => new LiteDatabase($"Filename={filesDatabasePath};Connection=shared"));
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ICurrentProjectService, CurrentProjectService>();
        services.AddSingleton<ILookupStore, LookupStore>();
        services.AddSingleton<ICommentService, CommentService>();
        services.AddSingleton<IDatasetService, DatasetService>();
        services.AddSingleton<IVariableService, VariableService>();
        services.AddSingleton<ICodeListService, CodeListService>();
        services.AddSingleton<ITermService, TermService>();
        services.AddSingleton<IDocumentService, DocumentService>();
        services.AddSingleton<IMethodService, MethodService>();
        services.AddSingleton<IReferenceDeletionService, ReferenceDeletionService>();
        services.AddSingleton<IProjectService, ProjectService>();
        services.AddSingleton<IValueLevelService, ValueLevelService>();
        services.AddSingleton<IIssueService, IssueService>();
        services.AddSingleton<IDefineIssueService, DefineIssueService>();
        services.AddSingleton<IDictionaryService, DictionaryService>();
        services.AddSingleton<IDefineExcelExportService, DefineExcelExportService>();
        services.AddSingleton<IDefineXmlExportService, DefineXmlExportService>();
        services.AddSingleton<IDefineValidator, DefineValidator>();
        services.AddSingleton<IDefineXmlValidationService, DefineXmlValidationService>();
        services.AddSingleton<IMessageService, MessageService>();
        services.AddTransient<IValidator<ProjectDto>, ProjectValidator>();
        services.AddTransient<IValidator<DatasetDto>, DatasetValidator>();
        services.AddTransient<IValidator<VariableDto>, VariableValidator>();
        services.AddTransient<IValidator<TermDto>, TermValidator>();
        services.AddTransient<IValidator<CommentDto>, CommentValidator>();
        services.AddTransient<IValidator<DocumentDto>, DocumentValidator>();
        services.AddTransient<IValidator<CodeListDto>, CodeListValidator>();
        services.AddTransient<IValidator<MethodDto>, MethodValidator>();
        services.AddTransient<IValidator<ValueLevelDto>, ValueLevelValidator>();
        services.AddTransient<IValidator<DictionaryDto>, DictionaryValidator>();
        services.AddTransient<PairCodeListValidator>();
        services.AddTransient<FormMethodValidator>();
        services.AddTransient<FormValueLevelValidator>();
        services.AddTransient<FormProjectValidator>();
        services.AddTransient<FormDictionaryValidator>();
        services.AddTransient<FormCommentValidator>();
        services.AddTransient<FormDocumentValidator>();

        var serviceProvider = services.BuildServiceProvider();
        _ = serviceProvider.GetRequiredService<ILookupStore>();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow { DataContext = serviceProvider.GetRequiredService<MainWindowViewModel>() };
            desktop.MainWindow = window;
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            var mainWindowViewModel = (MainWindowViewModel)window.DataContext;
            mainWindowViewModel.SelectedNavMenuItem = mainWindowViewModel.NavMenuItems[0];
        }
    }
}
