using System.ComponentModel;
using BugReporter.Serialization;
using GitExtensions.Extensibility;
using GitExtensions.Xplat.Core.Diagnostics;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

/// <summary>
///  Upstream's <c>BugReportInvoker</c>, <c>UIReporter</c> and <c>ExceptionDetails</c> rules, as the new shell's error
///  reports use them.
/// </summary>
internal sealed class ErrorReportsTests
{
    private const string DubiousOutput = """
                                         fatal: detected dubious ownership in repository at '/home/other/repo'
                                         To add an exception for this directory, call:

                                         	git config --global --add safe.directory /home/other/repo
                                         """;

    [Test]
    public void Classify_should_show_nothing_for_a_cancellation()
    {
        ErrorReports.Classify(new OperationCanceledException()).Kind.Should().Be(ErrorReportKind.None);
        ErrorReports.Classify(new TaskCanceledException()).Kind.Should().Be(ErrorReportKind.None);
    }

    [Test]
    public void Classify_should_offer_a_bug_report_for_an_error_of_the_application()
    {
        ErrorReport report =
            ErrorReports.Classify(new InvalidOperationException("outer", new ArgumentException("inner")));

        report.Kind.Should().Be(ErrorReportKind.Error);
        report.RootError.Should().Be("inner");
        report.IsExternalOperation.Should().BeFalse();
        report.IsUserExternalOperation.Should().BeFalse();
    }

    [Test]
    public void Classify_should_treat_git_errors_as_the_users()
    {
        ErrorReport report = ErrorReports.Classify(new GitOperationException("fatal: bad revision"));

        report.Kind.Should().Be(ErrorReportKind.Error);
        report.IsExternalOperation.Should().BeTrue();
        report.IsUserExternalOperation.Should().BeTrue();
    }

    [Test]
    public void Classify_should_treat_file_and_OS_errors_as_external_but_reportable()
    {
        foreach (Exception exception in new Exception[] { new IOException("disk"), new Win32Exception(5) })
        {
            ErrorReport report = ErrorReports.Classify(exception);
            report.IsExternalOperation.Should().BeTrue();
            report.IsUserExternalOperation.Should().BeFalse();
        }
    }

    [Test]
    public void Classify_should_find_a_file_that_failed_to_load_even_inside_another_exception()
    {
        FileNotFoundException notLoaded = new("Could not load file or assembly 'Foo.Bar, Version=1.0'",
            "Foo.Bar, Version=1.0");

        ErrorReports.Classify(new InvalidOperationException("wrapper", notLoaded)).Kind
            .Should().Be(ErrorReportKind.FailedToLoadAnAssembly);
        ErrorReports.FailedFileName(notLoaded).Should().Be("Foo.Bar");
        ErrorReports.CanReportFailedFile("Foo.Bar").Should().BeTrue();
        ErrorReports.CanReportFailedFile("System.Text.Json").Should().BeFalse();
        ErrorReports.CanReportFailedFile("vcruntime140_cor3.dll").Should().BeFalse();
        ErrorReports.FailedFileName(new DllNotFoundException("Unable to load DLL 'vcruntime140_cor3.dll' or one"))
            .Should().Be("vcruntime140_cor3.dll");
    }

    [Test]
    public void Classify_should_recognise_gits_refusal_of_a_repository_it_does_not_trust()
    {
        ErrorReports.Classify(new GitOperationException(DubiousOutput)).Kind.Should()
            .Be(ErrorReportKind.DubiousOwnership);
        ErrorReports.Classify(new ExternalOperationException("git", "status", "/home/other/repo", 128,
                new Exception(DubiousOutput))).Kind
            .Should().Be(ErrorReportKind.DubiousOwnership);
    }

    [Test]
    public void DubiousOwnershipIn_should_read_the_command_and_folder_from_gits_output()
    {
        DubiousOwnership dubious = ErrorReports.DubiousOwnershipIn(DubiousOutput)!;

        dubious.TrustCommand.Should().Be("config --global --add safe.directory /home/other/repo");
        dubious.Folder.Should().Be("/home/other/repo");
        dubious.GitOutput.Should().StartWith("fatal: detected dubious ownership");
        ErrorReports.DubiousOwnershipIn("fatal: not a git repository").Should().BeNull();
        ErrorReports.DubiousOwnershipIn(null).Should().BeNull();
    }

    [Test]
    public void DubiousOwnershipIn_should_prefer_the_failed_commands_folder_and_quote_a_path_as_upstream()
    {
        DubiousOwnership dubious = ErrorReports.DubiousOwnershipIn(
            "git config --global --add safe.directory 'd:/folder/repo with space'", @"d:\folder\repo with space")!;

        dubious.Folder.Should().Be(@"d:\folder\repo with space");
        dubious.TrustCommand.Should().Be(OperatingSystem.IsWindows()
            ? "config --global --add safe.directory \"d:/folder/repo with space\""
            : "config --global --add safe.directory 'd:/folder/repo with space'");
        ErrorReports.DubiousOwnershipIn("git config --global --add safe.directory '/a b'")!.Folder.Should().Be("/a b");
    }

    [Test]
    public void ExceptionInfo_should_list_the_failed_commands_details_as_upstream()
    {
        ExternalOperationException failed = new("git", "fetch origin", "/repo", 128, new Exception("fatal"));

        ErrorReports.ExceptionInfo(failed, new ExceptionInfoLabels()).ReplaceLineEndings("\n").Should().Be(
            "Exit code: 128\nCommand: git\nArguments: fetch origin\nWorking directory: /repo\n");
        ErrorReports.ExceptionInfo(new InvalidOperationException(), new ExceptionInfoLabels()).Should().BeEmpty();
        ErrorReports.ExceptionInfo(new UserExternalOperationException("Script failed", failed),
                new ExceptionInfoLabels())
            .Should().StartWith("Script failed");
    }

    [Test]
    public void LogError_should_write_the_exception_to_a_file_named_after_the_application()
    {
        string? file = ErrorReports.LogError(new InvalidOperationException("logged"), isTerminating: true,
            out string? error);
        try
        {
            error.Should().BeNull();
            Path.GetFileName(file).Should().StartWith("GitExtensions.");
            File.ReadAllText(file!).Should().StartWith("Is fatal: True").And.Contain("logged");
        }
        finally
        {
            File.Delete(file!);
        }
    }

    [Test]
    public void ExceptionDetails_should_list_each_exception_in_a_tree_and_its_properties_in_upstreams_order()
    {
        InvalidOperationException exception = new("outer", new AggregateException(new IOException("first"),
            new ArgumentException("second")));
        exception.Data["key"] = "value";
        SerializableException serializable = new(exception);

        ExceptionNode root = ExceptionDetails.Tree(serializable);

        root.Type.Should().Be("System.InvalidOperationException");
        ExceptionNode aggregate = root.Children.Should().ContainSingle().Subject;
        aggregate.Type.Should().Be("System.AggregateException");
        aggregate.Children.Select(child => child.Type).Should().Contain("System.IO.IOException")
            .And.Contain("System.ArgumentException");
        IReadOnlyList<ExceptionProperty> rows = ExceptionDetails.Properties(serializable);
        rows.Select(row => row.Property).Should().Equal(
            "Exception", "Message", "Inner Exception", "Data[\"key\"]", "HResult");
        rows.Where(row => row.IsExtended).Select(row => row.Property).Should().Equal("HResult");
    }

    [Test]
    public void NewIssueAddress_should_open_a_new_issue_titled_with_the_message()
    {
        SerializableException exception = new(new InvalidOperationException("boom"));

        string address = ErrorReports.NewIssueAddress(exception, "info", "- Git Extensions 1.0", "steps")!;

        address.Should().StartWith(ErrorReports.NewIssueUrl + "?title=%5BNBug%5D%20boom&template=bug_report.yml");
        address.Should().Contain("steps");
        ErrorReports.CopyText(exception, "info", "- Git Extensions 1.0", "steps").Should()
            .StartWith("- Git Extensions 1.0")
            .And.Contain("boom").And.EndWith("steps\n");
    }

    [Test]
    public void ContainsInfo_should_need_more_than_white_space()
    {
        ErrorReports.ContainsInfo(" \r\n\t").Should().BeFalse();
        ErrorReports.ContainsInfo(" x ").Should().BeTrue();
    }
}
