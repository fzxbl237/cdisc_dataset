using System.Collections.Generic;
using AtomUI;
using AtomUI.Controls;
using AtomUI.Desktop.Controls;
using AtomUI.Theme;
using AtomUI.Theme.Language;
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
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Mvvm;
using SqlSugar;
using DbType = System.Data.DbType;
using VariableCodeList = cdisc_dataset.Models.Settings.VariableCodeList;
using Window = AtomUI.Desktop.Controls.Window;

namespace cdisc_dataset;

public class App : PrismApplication
{
    public override void Initialize()
    {
        base.Initialize();
        AvaloniaXamlLoader.Load(this);
        this.UseAtomUI(builder =>
        {
            builder.WithDefaultLanguageVariant(LanguageVariant.zh_CN);
            builder.WithDefaultTheme(IThemeManager.DEFAULT_THEME_ID);
            builder.UseAlibabaSansFont();
            builder.UseDesktopControls();
            builder.UseDesktopDataGrid();
        });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();
        //this.AttachDevTools();
        var window = Container.Resolve<MainWindow>();
        var windowDataContext = window.DataContext;
        if (windowDataContext is MainWindowViewModel mainWindowViewModel)
        {
            mainWindowViewModel.SelectedNavMenuItem = mainWindowViewModel.NavMenuItems[0];
        }
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
    
    
    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.Register<MainWindow>();
        containerRegistry.RegisterForNavigation<ProjectView, ProjectViewModel>("Projects");
        containerRegistry.RegisterForNavigation<SdtmDefineView, SdtmDefineViewModel>("SdtmDefine");
        containerRegistry.RegisterForNavigation<TerminologyView, TerminologyViewModel>("Terminology");
        containerRegistry.RegisterForNavigation<FileView, FileViewModel>("Files");
        containerRegistry.Register<IDialogHostService, DialogHostService>();
        containerRegistry.RegisterForNavigation<ProjectDialog,EditProjectViewModel>("ProjectDialog");
        containerRegistry.RegisterForNavigation<DatasetDialog,DatasetViewModel>();
        containerRegistry.RegisterForNavigation<CommentDialog,CommentViewModel>("CommentDialog");
        containerRegistry.RegisterForNavigation<DictionaryDialog,DictionaryViewModel>("DictionaryDialog");
        containerRegistry.RegisterForNavigation<MethodDialog,MethodViewModel>("MethodDialog");
        containerRegistry.RegisterForNavigation<WhereClauseEditorDialog,WhereClauseEditorViewModel>("WhereClauseEditorDialog");
        containerRegistry.RegisterForNavigation<DatasetDialog,DatasetViewModel>("DatasetDialog");
        containerRegistry.RegisterForNavigation<VariableDialog,VariableViewModel>("VariableDialog");
        containerRegistry.RegisterForNavigation<DeleteCommentDialog,DeleteCommentViewModel>("DeleteCommentDialog");
        containerRegistry.RegisterForNavigation<EditKeyVariablesDialog,EditKeyVariablesViewModel>("EditKeyVariables");
        containerRegistry.RegisterForNavigation<AddCodeListDialog,AddCodeListViewModel>("AddCodeListDialog");
        containerRegistry.RegisterForNavigation<AddTermsDialog,AddTermsViewModel>("AddTermsDialog");
        containerRegistry.RegisterForNavigation<PairTermsDialog,PairTermsViewModel>("PairTermsDialog");
        containerRegistry.RegisterForNavigation<UnsavedChangesDialog,UnsavedChangesViewModel>("UnsavedChangesDialog");
        containerRegistry.RegisterForNavigation<ConfirmDialog,ConfirmViewModel>("ConfirmDialog");
        
        containerRegistry.RegisterForNavigation<CommentsView,CommentsViewModel>("Comments");
        containerRegistry.RegisterForNavigation<DocumentsView,DocumentsViewModel>("Documents");
        containerRegistry.RegisterForNavigation<MethodsView,MethodsViewModel>("Methods");
        containerRegistry.RegisterForNavigation<ValueLevelsView,ValueLevelsViewModel>("ValueLevels");
        containerRegistry.RegisterForNavigation<CodeListView,CodeListViewModel>("CodeLists");
        containerRegistry.RegisterForNavigation<TermView,TermViewModel>("Terms");     
        containerRegistry.RegisterForNavigation<VariablesView,VariablesViewModel>("Variables");
        containerRegistry.RegisterForNavigation<DatasetsView,DatasetsViewModel>("Datasets");
        containerRegistry.RegisterForNavigation<DictionariesView,DictionariesViewModel>("Dictionaries");


        var config = new TypeAdapterConfig();
        config.NewConfig<Dataset, Dataset>();
        containerRegistry.RegisterInstance(config);
        containerRegistry.RegisterSingleton<IMapper, Mapper>();
        containerRegistry.RegisterSingleton<ISqlSugarClient>(s =>
        {
            
            var sqlSugar = new SqlSugarClient([
                
                new ConnectionConfig()
                {
                    ConfigId = "project", DbType = SqlSugar.DbType.Sqlite,
                    ConnectionString = "DataSource=cdisc_dataset.db",
                    IsAutoCloseConnection = true, InitKeyType = InitKeyType.Attribute
                },

                new ConnectionConfig()
                {
                    ConfigId = "setting", DbType = SqlSugar.DbType.Sqlite,
                    ConnectionString = "DataSource=cdisc_setting.db", IsAutoCloseConnection = true
                }
            ]);
            
            // SqlSugarScope sqlSugar = new SqlSugarScope(new ConnectionConfig()
            // {
            //     DbType = SqlSugar.DbType.Sqlite,
            //     ConnectionString = "DataSource=cdisc_dataset.db",
            //     IsAutoCloseConnection = true,
            //     InitKeyType = InitKeyType.Attribute,
            // });
            
            var sqlSugarProject = sqlSugar.GetConnection("project");

            sqlSugarProject.CodeFirst.InitTables<Project, Document, Dataset, Variable>();
            sqlSugarProject.CodeFirst.InitTables<CodeList, Term, Comment, Method, ValueLevel>();
            sqlSugarProject.CodeFirst.InitTables<Dictionary, Issue,WhereClause,DictionaryVersion>();
            var sqlSugarSetting = sqlSugar.GetConnection("setting");
            sqlSugarSetting.CodeFirst.InitTables<VariableCodeList,CodeListTerm>();
            FixHasErrorsDefault(sqlSugar);
            return sqlSugar;
        });
        containerRegistry.RegisterSingleton<ILiteDatabase>(_ => new LiteDatabase("Filename=cdisc_files.db;Connection=shared"));
        containerRegistry.RegisterSingleton<ICurrentProjectService, CurrentProjectService>();
        containerRegistry.RegisterSingleton<ICommentService, CommentService>();
        containerRegistry.RegisterSingleton<IDatasetService, DatasetService>();
        containerRegistry.RegisterSingleton<IVariableService, VariableService>();
        containerRegistry.RegisterSingleton<ICodeListService, CodeListService>();
        containerRegistry.RegisterSingleton<ITermService, TermService>();
        containerRegistry.RegisterSingleton<IDocumentService, DocumentService>();
        containerRegistry.RegisterSingleton<IMethodService, MethodService>();
        containerRegistry.RegisterSingleton<IProjectService, ProjectService>();
        containerRegistry.RegisterSingleton<IValueLevelService, ValueLevelService>();
        containerRegistry.RegisterSingleton<IIssueService, IssueService>();
        containerRegistry.RegisterSingleton<IDictionaryService, DictionaryService>();
        containerRegistry.RegisterSingleton<IMessageService, MessageService>();      
        containerRegistry.Register<IValidator<ProjectDto>,ProjectValidator>();
        containerRegistry.Register<IValidator<DatasetDto>,DatasetValidator>();
        containerRegistry.Register<IValidator<VariableDto>,VariableValidator>();
        containerRegistry.Register<IValidator<TermDto>,TermValidator>();
        containerRegistry.Register<IValidator<CommentDto>,CommentValidator>();
        containerRegistry.Register<IValidator<DocumentDto>,DocumentValidator>();
        containerRegistry.Register<IValidator<CodeListDto>,CodeListValidator>();
        containerRegistry.Register<IValidator<MethodDto>,MethodValidator>();
        containerRegistry.Register<IValidator<ValueLevelDto>,ValueLevelValidator>();
        containerRegistry.Register<IValidator<DictionaryDto>,DictionaryValidator>();

        // form validator
        containerRegistry.Register<PairCodeListValidator>();
        containerRegistry.Register<FormMethodValidator>();
        containerRegistry.Register<FormValueLevelValidator>();
        containerRegistry.Register<FormProjectValidator>();
        containerRegistry.Register<FormDictionaryValidator>();
    }
    
    protected override AvaloniaObject CreateShell()
    {
        this.UseAtomUI(builder =>
        {
            builder.WithDefaultLanguageVariant(LanguageVariant.zh_CN);
            builder.WithDefaultTheme(IThemeManager.DEFAULT_THEME_ID);
            builder.UseAlibabaSansFont();
            builder.UseDesktopControls();
            builder.UseDesktopDataGrid();
        });
        var window = Container.Resolve<MainWindow>();
        // var topLevel = TopLevel.GetTopLevel(window);
        // var windowMessageManager = new WindowMessageManager(topLevel);
        // windowMessageManager.MaxItems = 10;
        // _containerRegistry.RegisterInstance(windowMessageManager);
        return window;
    }
}
