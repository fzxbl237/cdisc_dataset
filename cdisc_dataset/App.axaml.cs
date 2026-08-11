using System.Collections.Generic;
using AtomUI;
using AtomUI.Controls;
using AtomUI.Desktop.Controls;
using AtomUI.Theme;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Settings;
using cdisc_dataset.Services;
using cdisc_dataset.Services.Interface;
using cdisc_dataset.Validations;
using cdisc_dataset.Validations.Form;
using cdisc_dataset.ViewModels;
using cdisc_dataset.ViewModels.Defines;
using cdisc_dataset.ViewModels.Dialogs;
using cdisc_dataset.Views;
using cdisc_dataset.Views.Defines;
using cdisc_dataset.Views.Dialogs;
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
using DbType = System.Data.DbType;
using VariableCodeList = cdisc_dataset.Models.Settings.VariableCodeList;
using Window = AtomUI.Desktop.Controls.Window;

namespace cdisc_dataset;

public class App : Application
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

    private static void FixHasErrorsDefault(ISqlSugarClient sqlSugar)
    {
        sqlSugar.Ado.ExecuteCommand("UPDATE Project SET HasErrors = COALESCE(HasErrors, 0)");
        sqlSugar.Ado.ExecuteCommand("UPDATE Document SET HasErrors = COALESCE(HasErrors, 0)");
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
    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();

        var services = new ServiceCollection();
        services.AddNavigationSupport()
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
            .AddSingleton<IDialogHostService, DialogHostService>();

        RegisterDialog<ProjectDialog, EditProjectViewModel>(services, "ProjectDialog");
        RegisterDialog<ImportSettingDatasetsDialog, ImportSettingDatasetsViewModel>(services, "ImportSettingDatasetsDialog");
        RegisterDialog<CommentDialog, CommentViewModel>(services, "CommentDialog");
        RegisterDialog<DocumentDialog, DocumentViewModel>(services, "DocumentDialog");
        RegisterDialog<DictionaryDialog, DictionaryViewModel>(services, "DictionaryDialog");
        RegisterDialog<MethodDialog, MethodViewModel>(services, "MethodDialog");
        RegisterDialog<WhereClauseEditorDialog, WhereClauseEditorViewModel>(services, "WhereClauseEditorDialog");
        RegisterDialog<VariableDialog, VariableViewModel>(services, "VariableDialog");
        RegisterDialog<DeleteCommentDialog, DeleteCommentViewModel>(services, "DeleteCommentDialog");
        RegisterDialog<EditKeyVariablesDialog, EditKeyVariablesViewModel>(services, "EditKeyVariables");
        RegisterDialog<AddCodeListDialog, AddCodeListViewModel>(services, "AddCodeListDialog");
        RegisterDialog<AddTermsDialog, AddTermsViewModel>(services, "AddTermsDialog");
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
                new ConnectionConfig { ConfigId = "project", DbType = SqlSugar.DbType.Sqlite, ConnectionString = "DataSource=cdisc_dataset.db", IsAutoCloseConnection = true, InitKeyType = InitKeyType.Attribute },
                new ConnectionConfig { ConfigId = "setting", DbType = SqlSugar.DbType.Sqlite, ConnectionString = "DataSource=cdisc_setting.db", IsAutoCloseConnection = true }
            ]);
            var sqlSugarProject = sqlSugar.GetConnection("project");
            sqlSugarProject.CodeFirst.InitTables<Project, Document, Dataset, Variable>();
            sqlSugarProject.CodeFirst.InitTables<CodeList, Term, Comment, Method, ValueLevel>();
            sqlSugarProject.CodeFirst.InitTables<Dictionary, Issue, WhereClause, DictionaryVersion>();
            sqlSugar.GetConnection("setting").CodeFirst.InitTables<VariableCodeList, CodeListTerm, 
                CodeListReference,DatasetTemplate,VariableTemplate>();
            FixHasErrorsDefault(sqlSugar);
            return sqlSugar;
        });
        services.AddSingleton<ILiteDatabase>(_ => new LiteDatabase("Filename=cdisc_files.db;Connection=shared"));
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ICurrentProjectService, CurrentProjectService>();
        services.AddSingleton<ICommentService, CommentService>();
        services.AddSingleton<IDatasetService, DatasetService>();
        services.AddSingleton<IVariableService, VariableService>();
        services.AddSingleton<ICodeListService, CodeListService>();
        services.AddSingleton<ITermService, TermService>();
        services.AddSingleton<IDocumentService, DocumentService>();
        services.AddSingleton<IMethodService, MethodService>();
        services.AddSingleton<IProjectService, ProjectService>();
        services.AddSingleton<IValueLevelService, ValueLevelService>();
        services.AddSingleton<IIssueService, IssueService>();
        services.AddSingleton<IDictionaryService, DictionaryService>();
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
