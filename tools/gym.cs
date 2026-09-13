#:package Spectre.Console
#:property PublishAot=false
// The gym command: find, read, run and reset the exercises in this repository.
// From the repository root, type  .\gym  for a menu, or  .\gym help  for the commands.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Spectre.Console;

Console.OutputEncoding = Encoding.UTF8;

var root = FindRoot();
if (root is null)
{
    AnsiConsole.MarkupLine("[red]Could not find the repository root (the folder that contains InterviewGym.slnx).[/]");
    return 1;
}

const string launcher = @".\gym";
var exercises = Exercise.LoadAll(root);
var state = GymState.Load(root);
// Menus, spinners and screen clearing need a terminal that takes keyboard input and understands ANSI codes.
var interactive = AnsiConsole.Profile.Capabilities.Interactive && AnsiConsole.Profile.Capabilities.Ansi;

var command = args.Length > 0 ? args[0].ToLowerInvariant() : interactive ? "menu" : "help";
var rest = args.Skip(1).ToArray();

return command switch
{
    "menu" => Menu(),
    "next" => Next(),
    "progress" => Progress(),
    "list" or "ls" => List(rest),
    "show" or "read" => WithExercise(rest, e => { PrintBrief(e); PrintNextStep(e); return 0; }),
    "open" => WithExercise(rest, e => OpenExercise(e, rest.Contains("--choose"))),
    "test" => WithExercise(rest, e => RunTests(e, rest.Skip(1).Where(a => a != "--raw").ToList(), rest.Contains("--raw"))),
    "watch" => WithExercise(rest, WatchTests),
    "run" => WithExercise(rest, e => RunDemo(e, rest.Skip(1).ToList())),
    "done" => WithExercise(rest, e => MarkDone(e, true)),
    "todo" => WithExercise(rest, e => MarkDone(e, false)),
    "reset" => WithExercise(rest, e => ResetExercise(e, rest.Contains("--yes"))),
    "server" => Server(rest),
    "index" => Index(),
    "help" or "-h" or "--help" or "/?" => Help(),
    _ => Unknown(command),
};

// ─── Commands ────────────────────────────────────────────────────────────────

int Help()
{
    AnsiConsole.MarkupLine("[bold].NET Interview Gym[/]");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine($"Type [bold]{Esc(launcher)}[/] on its own for a menu that guides you. Or use a command:");
    AnsiConsole.WriteLine();
    var table = new Table().NoBorder().HideHeaders().AddColumn("").AddColumn("");
    void Row(string cmd, string what) => table.AddRow($"[bold]{Esc(launcher)} {Esc(cmd)}[/]", Esc(what));
    Row("next", "Open the next exercise in the interview plan");
    Row("progress", "See what you have done, stage by stage");
    Row("list [filter]", "List exercises: list 07, list fix, list todo, list token");
    Row("show <id>", "Print an exercise's brief");
    Row("open <id>", "Open an exercise in your editor (--choose to pick a different editor)");
    Row("test <id>", "Run its tests (--raw shows the full dotnet test output)");
    Row("watch <id>", "Run its tests again every time you save a file");
    Row("run <id>", "Run its demo program, or the MAUI app");
    Row("done <id>", "Mark an exercise as done (todo <id> undoes it)");
    Row("reset <id>", "Undo your changes to an exercise (asks first; --yes skips that)");
    Row("server", "Start the mock platform on http://localhost:5080");
    Row("index", "Regenerate EXERCISES.md and the category READMEs");
    AnsiConsole.Write(table);
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine($"[grey]<id> is a number such as 07-02 (or 7-2), or a word from the exercise's name: {Esc(launcher)} test flaky[/]");
    return 0;
}

int Unknown(string name)
{
    AnsiConsole.MarkupLine($"[red]Unknown command '{Esc(name)}'.[/]");
    AnsiConsole.WriteLine();
    Help();
    return 1;
}

int Menu()
{
    while (true)
    {
        Screen();
        var next = NextExercise();
        var choices = new List<Choice>();
        if (next is not null) choices.Add(new($"Continue the plan: [bold]{next.Id}[/] {Esc(next.Title)}", "next"));
        choices.Add(new("Browse all exercises", "browse"));
        choices.Add(new("See my progress", "progress"));
        choices.Add(new("Start the mock platform", "server"));
        choices.Add(new("Show the commands behind this menu", "help"));
        choices.Add(new("[grey]Quit[/]", "quit"));

        switch (Pick("What would you like to do?", choices))
        {
            case "next": ExerciseScreen(next!); break;
            case "browse": Browse(); break;
            case "progress": Screen(); Progress(); Pause(); break;
            case "server": Server([]); break;
            case "help": Screen(); Help(); Pause(); break;
            default: return 0;
        }
    }
}

void Browse()
{
    while (true)
    {
        Screen();
        var categories = exercises.GroupBy(e => e.Category).Select(g => new Choice(
            $"{g.Key[..2]}  {Esc(Categories.Name(g.Key))}  [grey]{g.Count(e => state.IsDone(e.Id))} of {g.Count()} done[/]", g.Key)).ToList();
        categories.Add(new("[grey]← Back[/]", "back"));
        var category = Pick("Choose a category", categories, 18);
        if (category == "back") return;

        while (true)
        {
            Screen();
            var items = exercises.Where(e => e.Category == category).Select(e => new Choice(ExerciseLine(e), e.Id)).ToList();
            items.Add(new("[grey]← Back[/]", "back"));
            var id = Pick($"[bold]{category[..2]} · {Esc(Categories.Name(category))}[/]  [grey]{Esc(Categories.Blurb(category))}[/]", items, 12);
            if (id == "back") break;
            ExerciseScreen(exercises.First(e => e.Id == id));
        }
    }
}

void ExerciseScreen(Exercise e)
{
    while (true)
    {
        Screen();
        AnsiConsole.MarkupLine($"[bold]{e.Id}  {Esc(e.Title)}[/]");
        AnsiConsole.MarkupLine($"[grey]{Esc(e.KindLabel)} · {Esc(e.Difficulty)} · {Esc(e.ShortTime)} · {Esc(e.RelativeDir)}[/]");
        if (state.DoneOn(e.Id) is { } when) AnsiConsole.MarkupLine($"[green]✓ Done on {when:d MMMM}[/]");
        AnsiConsole.WriteLine();

        var actions = new List<Choice> { new("Read the brief", "read"), new("Open in my editor", "open") };
        if (e.Kind == ExerciseKind.Fix)
        {
            actions.Add(new("Run the tests", "test"));
            actions.Add(new("Watch the tests (run them again every time I save)", "watch"));
        }

        if (e.Kind == ExerciseKind.App) actions.Add(new("Start the MAUI app", "run"));
        else if (e.DemoProject is not null) actions.Add(new("Run the demo", "run"));
        actions.Add(state.IsDone(e.Id) ? new("Mark as not done", "todo") : new("Mark as done", "done"));
        actions.Add(new("Reset: undo my changes", "reset"));
        actions.Add(new("[grey]← Back[/]", "back"));

        var action = Pick("What would you like to do?", actions);
        AnsiConsole.WriteLine();
        switch (action)
        {
            case "read": Screen(); PrintBrief(e); Pause(); break;
            case "open": OpenExercise(e, false); Pause(); break;
            case "test": RunTests(e, [], false); Pause(); break;
            case "watch": WatchTests(e); break;
            case "run": RunDemo(e, []); Pause(); break;
            case "done": MarkDone(e, true); break;
            case "todo": MarkDone(e, false); break;
            case "reset": ResetExercise(e, false); Pause(); break;
            default: return;
        }
    }
}

int Next()
{
    var next = NextExercise();
    if (next is null)
    {
        AnsiConsole.MarkupLine("[green]Everything in the plan is done.[/]");
        return 0;
    }

    if (interactive)
    {
        ExerciseScreen(next);
        return 0;
    }

    var stage = Plan.For(exercises).First(s => s.Items.Contains(next)).Name;
    AnsiConsole.MarkupLine($"Next ({Esc(stage)}): [bold]{next.Id}[/] {Esc(next.Title)}");
    PrintNextStep(next);
    return 0;
}

int Progress()
{
    var table = new Table().Border(TableBorder.Rounded)
        .AddColumn("Stage")
        .AddColumn(new TableColumn("Done").RightAligned())
        .AddColumn("Exercises");
    foreach (var (name, items) in Plan.For(exercises))
    {
        var done = items.Count(e => state.IsDone(e.Id));
        var shown = name == Plan.Rest ? items.Where(e => state.IsDone(e.Id)).ToList() : items;
        var marks = shown.Count == 0
            ? "[grey]none yet[/]"
            : string.Join("  ", shown.Select(e => state.IsDone(e.Id) ? $"[green]✓ {e.Id}[/]" : $"[grey]{e.Id}[/]"));
        table.AddRow(Esc(name), $"{done} of {items.Count}", marks);
    }

    AnsiConsole.Write(table);
    AnsiConsole.MarkupLine($"[bold]{exercises.Count(e => state.IsDone(e.Id))} of {exercises.Count}[/] exercises done.");
    if (NextExercise() is { } next) AnsiConsole.MarkupLine($"Next: [bold]{next.Id}[/] {Esc(next.Title)}  [grey]({Esc(launcher)} next)[/]");
    return 0;
}

int List(string[] filters)
{
    var filter = string.Join(' ', filters).Trim().ToLowerInvariant();
    var matches = exercises.Where(e => filter switch
    {
        "done" => state.IsDone(e.Id),
        "todo" => !state.IsDone(e.Id),
        _ => e.Matches(filter),
    }).ToList();

    if (matches.Count == 0)
    {
        AnsiConsole.MarkupLine($"No exercises match '{Esc(filter)}'. Run {Esc(launcher)} list to see them all.");
        return 1;
    }

    foreach (var group in matches.GroupBy(e => e.Category))
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]{group.Key[..2]}  {Esc(Categories.Name(group.Key))}[/]");
        var table = new Table().NoBorder().HideHeaders().AddColumns("", "", "", "", "", "");
        foreach (var e in group) table.AddRow(Mark(e), e.Id, Esc(e.KindLabel), Esc(e.Difficulty), Esc(e.ShortTime), Esc(e.Title));
        AnsiConsole.Write(table);
    }

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine($"{matches.Count} of {exercises.Count} exercises.  [grey]Open one: {Esc(launcher)} open 07-02   Run its tests: {Esc(launcher)} test 07-02[/]");
    return 0;
}

int MarkDone(Exercise e, bool done)
{
    state.SetDone(e.Id, done);
    AnsiConsole.MarkupLine(done ? $"[green]✓ {e.Id} is marked as done.[/]" : $"{e.Id} is marked as not done.");
    return 0;
}

int RunTests(Exercise e, IReadOnlyList<string> extra, bool raw)
{
    if (e.Kind != ExerciseKind.Fix)
    {
        AnsiConsole.MarkupLine($"{e.Id} has no tests: it is a {Esc(e.KindLabel.ToLowerInvariant())} exercise.");
        PrintNextStep(e);
        return 0;
    }

    var testsDir = Path.Combine(e.Dir, "tests");
    if (raw) return Exec("dotnet", ["test", testsDir, .. extra], root);

    var resultsDir = Path.Combine(Path.GetTempPath(), "gym-results", Guid.NewGuid().ToString("N"));
    (int Code, string Output) run = (0, "");
    AnsiConsole.MarkupLine($"[bold]{e.Id}  {Esc(e.Title)}[/]");
    Spin("Building and running the tests…", () =>
        run = Capture("dotnet", ["test", testsDir, "--logger", "trx;LogFileName=results.trx", "--results-directory", resultsDir, .. extra], root));

    var trx = Directory.Exists(resultsDir) ? Directory.GetFiles(resultsDir, "*.trx", SearchOption.AllDirectories).FirstOrDefault() : null;
    if (trx is null)
    {
        var errors = Regex.Matches(run.Output, @"^.*: error [A-Z]+\d+:.*$", RegexOptions.Multiline)
            .Select(m => ShortenBuildError(m.Value)).Distinct().ToList();
        if (errors.Count > 0)
        {
            AnsiConsole.MarkupLine("[red]✗ The code doesn't compile:[/]");
            foreach (var error in errors.Take(10)) AnsiConsole.MarkupLine($"  {Esc(error)}");
            if (errors.Count > 10) AnsiConsole.MarkupLine($"  [grey]…and {errors.Count - 10} more[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]✗ The tests didn't run. The end of the dotnet test output:[/]");
            AnsiConsole.WriteLine(string.Join('\n', run.Output.Split('\n').TakeLast(25)));
        }

        TryDelete(resultsDir);
        return run.Code == 0 ? 1 : run.Code;
    }

    XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
    var results = XDocument.Load(trx).Descendants(ns + "UnitTestResult").Select(r => (
        Name: TestSentence(r.Attribute("testName")?.Value ?? "?"),
        Outcome: r.Attribute("outcome")?.Value ?? "",
        Message: r.Element(ns + "Output")?.Element(ns + "ErrorInfo")?.Element(ns + "Message")?.Value))
        .OrderBy(r => r.Outcome == "Failed" ? 0 : r.Outcome == "Passed" ? 1 : 2).ThenBy(r => r.Name, StringComparer.Ordinal)
        .ToList();
    TryDelete(resultsDir);

    var passed = results.Count(r => r.Outcome == "Passed");
    var failed = results.Count(r => r.Outcome == "Failed");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine(failed == 0
        ? $"[green bold]✓ All {passed} tests pass[/]"
        : $"[red bold]✗ {passed} of {passed + failed} tests passing[/]");
    AnsiConsole.WriteLine();

    foreach (var r in results)
    {
        switch (r.Outcome)
        {
            case "Passed":
                AnsiConsole.MarkupLine($"  [green]✓[/] {Esc(r.Name)}");
                break;
            case "Failed":
                AnsiConsole.MarkupLine($"  [red]✗[/] {Esc(r.Name)}");
                foreach (var line in (r.Message ?? "").Split('\n').Select(l => l.TrimEnd()).Where(l => l.Length > 0).Take(3))
                    AnsiConsole.MarkupLine($"      [grey]{Esc(line)}[/]");
                break;
            default:
                AnsiConsole.MarkupLine($"  [grey]– {Esc(r.Name)} (skipped)[/]");
                break;
        }
    }

    AnsiConsole.WriteLine();
    if (failed == 0 && passed > 0)
    {
        // A few exercises start green because the work is writing tests or reshaping the design.
        // Passing without having changed anything is not finishing the exercise.
        var untouched = Capture("git", ["status", "--short", "--", .. e.OwnedPaths()], root) is { Code: 0 } status && status.Output.Trim().Length == 0;
        if (untouched)
        {
            AnsiConsole.MarkupLine($"[yellow]These tests pass before any changes: the work in this exercise is described in its brief ({Esc(launcher)} show {e.Id}).[/]");
            return 0;
        }

        if (!state.IsDone(e.Id))
        {
            state.SetDone(e.Id, true);
            AnsiConsole.MarkupLine($"[green]{e.Id} is marked as done.[/] Be ready to explain why your change is right.");
        }

        if (NextExercise() is { } next) AnsiConsole.MarkupLine($"Next in the plan: [bold]{next.Id}[/] {Esc(next.Title)}  [grey]({Esc(launcher)} next)[/]");
        return 0;
    }

    if (!state.IsDone(e.Id)) AnsiConsole.MarkupLine("[grey]Red is where most exercises start: the failing tests describe the problem.[/]");
    return 1;
}

int WatchTests(Exercise e)
{
    if (e.Kind != ExerciseKind.Fix) return RunTests(e, [], false);

    var gate = new object();
    var pending = false;
    var lastChange = DateTime.MinValue;
    void OnChange(string path)
    {
        var parts = Path.GetRelativePath(e.Dir, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (parts.Any(p => p is "bin" or "obj" or "TestResults")) return;
        if (Path.GetExtension(path).ToLowerInvariant() is not (".cs" or ".xaml" or ".csproj" or ".json")) return;
        lock (gate) { pending = true; lastChange = DateTime.UtcNow; }
    }

    using var watcher = new FileSystemWatcher(e.Dir) { IncludeSubdirectories = true };
    watcher.Changed += (_, a) => OnChange(a.FullPath);
    watcher.Created += (_, a) => OnChange(a.FullPath);
    watcher.Deleted += (_, a) => OnChange(a.FullPath);
    watcher.Renamed += (_, a) => OnChange(a.FullPath);
    watcher.EnableRaisingEvents = true;

    while (true)
    {
        if (interactive) AnsiConsole.Clear();
        RunTests(e, [], false);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[grey]Watching {Esc(e.RelativeDir)}. Save a file to run the tests again. Press Q to stop.[/]");
        lock (gate) pending = false;

        while (true)
        {
            if (interactive && Console.KeyAvailable && Console.ReadKey(true).Key is ConsoleKey.Q or ConsoleKey.Escape) return 0;
            bool go;
            lock (gate) go = pending && DateTime.UtcNow - lastChange > TimeSpan.FromMilliseconds(700);
            if (go) break;
            Thread.Sleep(150);
        }
    }
}

int RunDemo(Exercise e, IReadOnlyList<string> extra)
{
    if (e.Kind == ExerciseKind.App)
    {
        AnsiConsole.MarkupLine("Starting the MAUI app. The first build takes a while. Close the app window to come back.");
        return Exec("dotnet", ["run", "--project", Path.Combine(root, "exercises", "12-maui", "MauiGym"), "-f", "net10.0-windows10.0.19041.0", .. extra], root);
    }

    if (e.DemoProject is null)
    {
        AnsiConsole.MarkupLine($"{e.Id} has no demo program.");
        PrintNextStep(e);
        return 0;
    }

    return Exec("dotnet", ["run", "--project", e.DemoProject, .. extra], root);
}

int OpenExercise(Exercise e, bool choose)
{
    var editors = new List<Choice>();
    if (FindOnPath("code") is not null) editors.Add(new("VS Code", "code"));
    if (FindOnPath("cursor") is not null) editors.Add(new("Cursor", "cursor"));
    editors.Add(new("Visual Studio or Rider (a solution with just this exercise)", "solution"));

    var editor = state.Editor;
    if (editor is null || choose || !editors.Any(x => x.Key == editor))
    {
        editor = interactive ? Pick("Which editor do you use?", editors) : editors[0].Key;
        state.Editor = editor;
        state.Save();
        AnsiConsole.MarkupLine($"[grey]Remembered. To change it later: {Esc(launcher)} open {e.Id} --choose[/]");
    }

    try
    {
        if (editor is "code" or "cursor")
        {
            var folder = e.Kind == ExerciseKind.App ? Path.Combine(root, "exercises", "12-maui", "MauiGym") : e.Dir;
            ShellOpen(FindOnPath(editor)!, $"-n \"{folder}\" \"{e.ReadmePath}\"");
            AnsiConsole.MarkupLine($"Opened {e.Id} in {(editor == "code" ? "VS Code" : "Cursor")}.");
            return 0;
        }

        var projects = e.Projects(root);
        if (projects.Count == 0)
        {
            AnsiConsole.MarkupLine($"{e.Id} has no code to open. Its brief is {Esc(Path.GetRelativePath(root, e.ReadmePath))}.");
            return 0;
        }

        var solution = Path.Combine(root, ".gym", "solutions", $"{e.Id}.slnx");
        Directory.CreateDirectory(Path.GetDirectoryName(solution)!);
        var lines = projects.Select(p => $"  <Project Path=\"{Path.GetRelativePath(Path.GetDirectoryName(solution)!, p)}\" />");
        File.WriteAllText(solution, "<Solution>\n" + string.Join('\n', lines) + "\n</Solution>\n", new UTF8Encoding(false));
        ShellOpen(solution, "");
        AnsiConsole.MarkupLine($"Opened a solution with just {e.Id}'s projects. The brief is {Esc(Path.GetRelativePath(root, e.ReadmePath))}.");
        return 0;
    }
    catch (System.ComponentModel.Win32Exception)
    {
        AnsiConsole.MarkupLine($"[red]Windows couldn't open it with that editor.[/] Choose another with: {Esc(launcher)} open {e.Id} --choose");
        return 1;
    }
}

int ResetExercise(Exercise e, bool confirmed)
{
    if (Capture("git", ["rev-parse", "--is-inside-work-tree"], root).Code != 0)
    {
        AnsiConsole.MarkupLine("[red]Reset needs Git: this folder is not a Git repository.[/]");
        return 1;
    }

    var paths = e.OwnedPaths();
    var status = Capture("git", ["status", "--short", "--", .. paths], root);
    if (status.Output.Trim().Length == 0)
    {
        AnsiConsole.MarkupLine($"Nothing to reset: {e.Id} has no changes.");
        return 0;
    }

    AnsiConsole.MarkupLine($"Changes to {e.Id}:");
    AnsiConsole.WriteLine(status.Output.TrimEnd());
    if (!confirmed)
    {
        if (!interactive)
        {
            AnsiConsole.MarkupLine($"Run {Esc(launcher)} reset {e.Id} --yes to discard them.");
            return 1;
        }

        if (!AnsiConsole.Confirm($"Discard these changes and restore {e.Id} to its original state?", false))
        {
            AnsiConsole.MarkupLine("Left as it is.");
            return 0;
        }
    }

    var restore = Exec("git", ["restore", "--source=HEAD", "--staged", "--worktree", "--", .. paths], root);
    var clean = Exec("git", ["clean", "-fdq", "--", .. paths], root);
    if (restore == 0 && clean == 0) AnsiConsole.MarkupLine($"[green]{e.Id} is back to its original state.[/]");
    return restore != 0 ? restore : clean;
}

int Server(string[] a)
{
    AnsiConsole.MarkupLine("Starting the mock platform on http://localhost:5080. Press Ctrl+C to stop it.");
    return Exec("dotnet", ["run", "--project", Path.Combine(root, "shared", "MockServer", "Gym.MockServer"), .. a], root);
}

int Index()
{
    var marker = "<!-- Generated by `gym index` from each exercise's README. Edit those, then run it again. -->";
    var types = "**Fix**: code with failing tests to make pass. **Discussion**: a design problem to talk or sketch through. " +
                "**Code review**: a pull request to review. **MAUI app**: a screen in the MAUI app to fix.";

    var all = new StringBuilder();
    all.Append(marker).Append("\n# Exercises\n\n")
       .Append($"All {exercises.Count} exercises. The easiest way in is `{launcher}`, which opens a menu. These commands work too:\n\n")
       .Append("| Command | What it does |\n|---|---|\n")
       .Append($"| `{launcher} next` | Open the next exercise in the interview plan |\n")
       .Append($"| `{launcher} open 07-02` | Open an exercise in your editor |\n")
       .Append($"| `{launcher} test 07-02` | Run its tests |\n")
       .Append($"| `{launcher} watch 07-02` | Run its tests again every time you save |\n")
       .Append($"| `{launcher} progress` | See what you have done |\n\n")
       .Append(types).Append("\n");

    foreach (var group in exercises.GroupBy(e => e.Category))
    {
        var number = group.Key[..2];
        all.Append($"\n## {number} · {Categories.Name(group.Key)}\n\n{Categories.Blurb(group.Key)}\n\n");
        all.Append(MarkdownTable(group,e => $"exercises/{e.Category}/{e.Folder}/README.md"));

        var categoryReadme = Path.Combine(root, "exercises", group.Key, "README.md");
        if (File.Exists(categoryReadme) && !File.ReadAllText(categoryReadme).Contains("Generated by `gym index`"))
        {
            AnsiConsole.MarkupLine($"Kept the hand-written exercises/{group.Key}/README.md");
            continue;
        }

        var fix = group.FirstOrDefault(e => e.Kind == ExerciseKind.Fix);
        var hint = fix is not null
            ? $"From the repository root, `{launcher} open {fix.Id}` opens an exercise in your editor and `{launcher} test {fix.Id}` runs its tests."
            : $"From the repository root, `{launcher} show {group.First().Id}` prints an exercise's brief.";
        var section = new StringBuilder();
        section.Append(marker).Append($"\n# {number} · {Categories.Name(group.Key)}\n\n{Categories.Blurb(group.Key)}\n\n")
               .Append(MarkdownTable(group,e => $"{e.Folder}/README.md"))
               .Append($"\n{hint}\n\n")
               .Append("[All exercises](../../EXERCISES.md)\n");
        WriteText(categoryReadme, section.ToString());
    }

    WriteText(Path.Combine(root, "EXERCISES.md"), all.ToString());
    AnsiConsole.MarkupLine($"Wrote EXERCISES.md ({exercises.Count} exercises) and the category READMEs.");
    return 0;
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

int WithExercise(string[] a, Func<Exercise, int> action)
{
    var e = Resolve(a.FirstOrDefault(x => !x.StartsWith('-')));
    return e is null ? 1 : action(e);
}

Exercise? NextExercise() =>
    Plan.For(exercises).SelectMany(s => s.Items).FirstOrDefault(e => !state.IsDone(e.Id));

void Screen()
{
    if (!interactive) return;
    AnsiConsole.Clear();
    var done = exercises.Count(e => state.IsDone(e.Id));
    AnsiConsole.Write(new Rule($"[bold].NET Interview Gym[/]  [grey]{done} of {exercises.Count} done[/]") { Justification = Justify.Left });
    AnsiConsole.WriteLine();
}

string Pick(string title, List<Choice> choices, int pageSize = 10) =>
    AnsiConsole.Prompt(new SelectionPrompt<Choice>()
        .Title(title)
        .PageSize(Math.Max(3, pageSize))
        .MoreChoicesText("[grey](more below)[/]")
        .UseConverter(c => c.Label)
        .AddChoices(choices)).Key;

void Pause()
{
    if (!interactive) return;
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[grey]Press any key to go back.[/]");
    Console.ReadKey(true);
}

void Spin(string message, Action work)
{
    if (interactive) AnsiConsole.Status().Spinner(Spinner.Known.Dots).Start(message, _ => work());
    else
    {
        AnsiConsole.MarkupLine($"[grey]{Esc(message)}[/]");
        work();
    }
}

string Mark(Exercise e) => state.IsDone(e.Id) ? "[green]✓[/]" : "[grey]·[/]";

string ExerciseLine(Exercise e) =>
    $"{Mark(e)} {e.Id}  {Esc(e.Title)}  [grey]{Esc(e.KindLabel)} · {Esc(e.Difficulty)} · {Esc(e.ShortTime)}[/]";

void PrintBrief(Exercise e)
{
    var inCode = false;
    foreach (var line in File.ReadLines(e.ReadmePath))
    {
        if (line.StartsWith("```"))
        {
            inCode = !inCode;
            continue;
        }

        if (inCode) AnsiConsole.MarkupLine($"    [aqua]{Esc(line)}[/]");
        else if (line.StartsWith("# ")) AnsiConsole.MarkupLine($"[bold underline]{Esc(line[2..])}[/]");
        else if (line.StartsWith("## ")) AnsiConsole.MarkupLine($"[bold yellow]{Esc(line[3..])}[/]");
        else AnsiConsole.MarkupLine(InlineMarkdown(line));
    }
}

void PrintNextStep(Exercise e)
{
    AnsiConsole.WriteLine();
    var next = e.Kind switch
    {
        ExerciseKind.Fix => $"open it with {launcher} open {e.Id}, change the code in src, then run {launcher} test {e.Id}",
        ExerciseKind.App => $"find its code in the MAUI app (see exercises/12-maui/README.md), then start the app with {launcher} run {e.Id}",
        ExerciseKind.Review => $"read PR.md and the code in {e.RelativeDir}/src, then write your review in REVIEW_TEMPLATE.md",
        _ => e.DemoProject is not null
            ? $"talk or sketch it through; {launcher} run {e.Id} runs its demo. When you're done: {launcher} done {e.Id}"
            : $"talk or sketch it through: there is no code to run. When you're done: {launcher} done {e.Id}",
    };
    AnsiConsole.MarkupLine($"[grey]What to do: {Esc(next)}[/]");
}

Exercise? Resolve(string? arg)
{
    if (string.IsNullOrWhiteSpace(arg))
    {
        AnsiConsole.MarkupLine($"Which exercise? For example: {Esc(launcher)} test 07-02");
        return null;
    }

    var text = arg.Trim().ToLowerInvariant();
    var number = Regex.Match(text, @"^(\d{1,2})[-.](\d{1,2})$|^(\d{2})(\d{2})$");
    if (number.Success)
    {
        var major = number.Groups[1].Success ? number.Groups[1].Value : number.Groups[3].Value;
        var minor = number.Groups[2].Success ? number.Groups[2].Value : number.Groups[4].Value;
        var id = $"{int.Parse(major):00}-{int.Parse(minor):00}";
        var exact = exercises.FirstOrDefault(e => e.Id == id);
        if (exact is null) AnsiConsole.MarkupLine($"There is no exercise {id}. Run {Esc(launcher)} list to see them all.");
        return exact;
    }

    var found = exercises.Where(e => e.Folder.Contains(text) || e.Title.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
    if (found.Count == 1) return found[0];

    if (found.Count == 0)
    {
        AnsiConsole.MarkupLine($"No exercise matches '{Esc(arg)}'. Run {Esc(launcher)} list to see them all.");
    }
    else
    {
        AnsiConsole.MarkupLine($"'{Esc(arg)}' matches {found.Count} exercises. Use a number:");
        foreach (var e in found) AnsiConsole.MarkupLine($"  {e.Id}  {Esc(e.Title)}");
    }

    return null;
}

string ShortenBuildError(string line)
{
    var shorter = Regex.Replace(line.Trim(), @"\s*\[[^\[\]]+\.csproj[^\[\]]*\]$", "");
    return shorter.Replace(root + Path.DirectorySeparatorChar, "");
}

static string Esc(string text) => Markup.Escape(text);

static string InlineMarkdown(string line)
{
    var escaped = Markup.Escape(line);
    escaped = Regex.Replace(escaped, @"\*\*(.+?)\*\*", "[bold]$1[/]");
    return Regex.Replace(escaped, @"`([^`]+)`", "[aqua]$1[/]");
}

// "Ns.Class.Concurrent_updates_are_never_lost(workers: 4)" → "Concurrent updates are never lost (workers: 4)"
static string TestSentence(string testName)
{
    var paren = testName.IndexOf('(');
    var head = paren >= 0 ? testName[..paren] : testName;
    var arguments = paren >= 0 ? " " + testName[paren..] : "";
    var method = head[(head.LastIndexOf('.') + 1)..].Replace('_', ' ').Trim();
    if (method.Length > 0) method = char.ToUpperInvariant(method[0]) + method[1..];
    if (arguments.Length > 90) arguments = arguments[..87] + "…)";
    return method + arguments;
}

static string MarkdownTable(IEnumerable<Exercise> items, Func<Exercise, string> link)
{
    var sb = new StringBuilder("| Exercise | Type | Difficulty | Time |\n|---|---|---|---|\n");
    foreach (var e in items)
    {
        sb.Append($"| [{e.Id} {e.Title.Replace("|", "\\|")}]({link(e)}) | {e.KindLabel} | {e.Difficulty} | {e.ShortTime} |\n");
    }

    return sb.ToString();
}

static void WriteText(string path, string text) => File.WriteAllText(path, text, new UTF8Encoding(false));

static void TryDelete(string dir)
{
    try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    catch (IOException) { /* a leftover temp folder is harmless */ }
    catch (UnauthorizedAccessException) { }
}

static void ShellOpen(string file, string arguments)
{
    using var _ = Process.Start(new ProcessStartInfo(file, arguments) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden });
}

static string? FindOnPath(string name)
{
    var extensions = OperatingSystem.IsWindows()
        ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT").Split(';', StringSplitOptions.RemoveEmptyEntries)
        : [""];
    foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
    {
        foreach (var extension in extensions)
        {
            var candidate = Path.Combine(dir.Trim('"'), name + extension);
            if (File.Exists(candidate)) return candidate;
        }
    }

    return null;
}

static int Exec(string file, IEnumerable<string> arguments, string workingDirectory)
{
    var start = new ProcessStartInfo(file) { WorkingDirectory = workingDirectory, UseShellExecute = false };
    foreach (var argument in arguments) start.ArgumentList.Add(argument);
    try
    {
        using var process = Process.Start(start)!;
        process.WaitForExit();
        return process.ExitCode;
    }
    catch (System.ComponentModel.Win32Exception)
    {
        AnsiConsole.MarkupLine($"[red]Could not start '{Markup.Escape(file)}'. Is it installed and on your PATH?[/]");
        return 127;
    }
}

static (int Code, string Output) Capture(string file, IEnumerable<string> arguments, string workingDirectory)
{
    var start = new ProcessStartInfo(file)
    {
        WorkingDirectory = workingDirectory,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };
    foreach (var argument in arguments) start.ArgumentList.Add(argument);
    try
    {
        using var process = Process.Start(start)!;
        var errors = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output + errors.Result);
    }
    catch (System.ComponentModel.Win32Exception)
    {
        return (127, $"'{file}' is not installed.");
    }
}

static string? FindRoot()
{
    var candidates = new List<string>();
    if (Environment.GetEnvironmentVariable("GYM_ROOT") is { Length: > 0 } fromLauncher) candidates.Add(fromLauncher);
    candidates.Add(Directory.GetCurrentDirectory());

    foreach (var start in candidates)
    {
        for (var dir = new DirectoryInfo(Path.GetFullPath(start)); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "InterviewGym.slnx"))) return dir.FullName;
        }
    }

    return null;
}

// ─── Types ───────────────────────────────────────────────────────────────────

record Choice(string Label, string Key);

enum ExerciseKind { Fix, Discussion, Review, App }

sealed record Exercise(string Id, string Title, string Category, string Folder, string Dir, string Difficulty, string Time, ExerciseKind Kind, string? DemoProject)
{
    public string ReadmePath => Path.Combine(Dir, "README.md");

    public string RelativeDir => $"exercises/{Category}/{Folder}";

    public string ShortTime => Regex.Match(Time, @"^\s*(\d+(?:\s*[–-]\s*\d+)?)\s*min", RegexOptions.IgnoreCase) is { Success: true } t
        ? $"{t.Groups[1].Value} min"
        : Time;

    public string KindLabel => Kind switch
    {
        ExerciseKind.Fix => "Fix",
        ExerciseKind.Review => "Code review",
        ExerciseKind.App => "MAUI app",
        _ => "Discussion",
    };

    public bool Matches(string filter)
    {
        if (filter.Length == 0) return true;
        var f = filter.ToLowerInvariant();
        return f switch
        {
            "fix" or "tests" => Kind == ExerciseKind.Fix,
            "discussion" or "design" => Kind == ExerciseKind.Discussion,
            "review" => Kind == ExerciseKind.Review,
            "app" or "maui app" => Kind == ExerciseKind.App,
            _ => Id.StartsWith(f) || Category.Contains(f) || Folder.Contains(f) || Title.Contains(f, StringComparison.OrdinalIgnoreCase)
                 || Difficulty.Equals(f, StringComparison.OrdinalIgnoreCase),
        };
    }

    // Projects that make up this exercise, for a solution containing just the exercise.
    public List<string> Projects(string root)
    {
        if (Kind == ExerciseKind.App) return [Path.Combine(root, "exercises", "12-maui", "MauiGym", "MauiGym.csproj")];
        return Directory.EnumerateFiles(Dir, "*.csproj", SearchOption.AllDirectories)
            .Where(p => !IsBuildOutput(p))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
    }

    // Paths that `gym reset` restores. The MAUI app exercises keep their code inside the shared app.
    public IReadOnlyList<string> OwnedPaths()
    {
        var app = "exercises/12-maui/MauiGym";
        var extra = Id switch
        {
            "12-01" => new[] { $"{app}/Exercises/E1201DetailFlow", $"{app}/MauiProgram.cs", $"{app}/AppShell.xaml.cs" },
            "12-02" => new[] { $"{app}/Exercises/E1202MigrationList" },
            "12-03" => new[] { $"{app}/Exercises/E1203Lifecycle", $"{app}/App.xaml.cs" },
            _ => Array.Empty<string>(),
        };
        return [RelativeDir, .. extra];
    }

    static bool IsBuildOutput(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(s => s is "bin" or "obj");

    public static List<Exercise> LoadAll(string root)
    {
        var list = new List<Exercise>();
        foreach (var categoryDir in Directory.GetDirectories(Path.Combine(root, "exercises")).OrderBy(d => d, StringComparer.Ordinal))
        {
            var category = Path.GetFileName(categoryDir);
            if (!Regex.IsMatch(category, @"^\d{2}-")) continue;

            foreach (var dir in Directory.GetDirectories(categoryDir).OrderBy(d => d, StringComparer.Ordinal))
            {
                var folder = Path.GetFileName(dir);
                var readme = Path.Combine(dir, "README.md");
                if (!Regex.IsMatch(folder, @"^\d{2}-\d{2}-") || !File.Exists(readme)) continue;

                var lines = File.ReadAllLines(readme);
                var heading = lines.FirstOrDefault(l => l.StartsWith("# ")) ?? folder;
                var title = Regex.Match(heading, @"^#\s*Exercise\s+\d{2}-\d{2}\s*[–-]\s*(.+)$") is { Success: true } m
                    ? m.Groups[1].Value.Trim()
                    : heading.TrimStart('#', ' ');
                string Field(string name) =>
                    lines.Select(l => Regex.Match(l, $@"^{name}:\s*(.+)$")).FirstOrDefault(x => x.Success)?.Groups[1].Value.Trim() ?? "";

                var kind = Directory.Exists(Path.Combine(dir, "tests")) ? ExerciseKind.Fix
                    : category == "12-maui" ? ExerciseKind.App
                    : title.StartsWith("Review", StringComparison.OrdinalIgnoreCase) ? ExerciseKind.Review
                    : ExerciseKind.Discussion;

                var demo = Directory.EnumerateFiles(dir, "*.csproj", SearchOption.AllDirectories)
                    .Where(p => !IsBuildOutput(p))
                    .FirstOrDefault(p => Regex.IsMatch(File.ReadAllText(p), @"<OutputType>\s*(Win)?Exe\s*</OutputType>", RegexOptions.IgnoreCase));

                list.Add(new Exercise(folder[..5], title, category, folder, dir, Field("Difficulty"), Field("Estimated Time"), kind, demo));
            }
        }

        return list;
    }
}

// Saved in .gym/state.json (ignored by Git): which exercises are done, and the chosen editor.
sealed class GymState
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public Dictionary<string, DateTime> Done { get; set; } = new();

    public string? Editor { get; set; }

    [JsonIgnore]
    public string FilePath { get; private set; } = "";

    public static GymState Load(string root)
    {
        var path = Path.Combine(root, ".gym", "state.json");
        GymState? loaded = null;
        try
        {
            if (File.Exists(path)) loaded = JsonSerializer.Deserialize<GymState>(File.ReadAllText(path), Json);
        }
        catch (JsonException)
        {
            // A damaged state file means starting the progress again, not a broken gym.
        }

        loaded ??= new GymState();
        loaded.FilePath = path;
        return loaded;
    }

    public bool IsDone(string id) => Done.ContainsKey(id);

    public DateTime? DoneOn(string id) => Done.TryGetValue(id, out var when) ? when : null;

    public void SetDone(string id, bool done)
    {
        if (done) Done.TryAdd(id, DateTime.Now);
        else Done.Remove(id);
        Save();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
    }
}

// The order of exercises in PREP_PLAN.md. Keep the two in step.
static class Plan
{
    public const string Rest = "Everything else";

    static readonly (string Name, string[] Ids)[] Stages =
    {
        ("Brillio interview", new[] { "01-01", "02-01", "02-02", "03-02" }),
        ("Sage technical round", new[] { "09-04", "07-01", "07-02", "10-04", "10-03", "02-04", "08-02", "06-01", "10-01", "11-02" }),
        ("Sage technical round, if there's time", new[] { "09-01", "09-03", "09-05", "08-01" }),
        ("Sage design round", new[] { "16-01", "16-05", "16-03", "16-06" }),
        ("MAUI", new[] { "12-01", "12-04" }),
    };

    public static List<(string Name, List<Exercise> Items)> For(IReadOnlyList<Exercise> all)
    {
        var byId = all.ToDictionary(e => e.Id);
        var stages = Stages
            .Select(s => (s.Name, s.Ids.Where(byId.ContainsKey).Select(id => byId[id]).ToList()))
            .ToList();
        var planned = Stages.SelectMany(s => s.Ids).ToHashSet();
        stages.Add((Rest, all.Where(e => !planned.Contains(e.Id)).ToList()));
        return stages;
    }
}

static class Categories
{
    static readonly Dictionary<string, (string Name, string Blurb)> Known = new()
    {
        ["01-csharp"] = ("C# / .NET", "Equality and keys, abstraction, modelling failure as data."),
        ["02-debugging"] = ("Debugging", "Six support tickets: fire-and-forget, swallowed errors, leaks, deadlock, stale state, wrong headers."),
        ["03-async"] = ("Async / await", "Bounded concurrency, cancellation that actually cancels, partial failure."),
        ["04-concurrency"] = ("Concurrency", "Races across `await`, double-start, a pipeline with backpressure."),
        ["05-http-api"] = ("HTTP / API", "Building a client, and surviving a server upgrade."),
        ["06-authentication"] = ("Authentication", "Token expiry mid-migration, secrets in logs, a token cache."),
        ["07-file-transfer"] = ("File transfer", "100 GB files, resuming, idempotency, real-world data."),
        ["08-reliability"] = ("Reliability", "Retry classification, long-running operations, retry storms."),
        ["09-sdk-design"] = ("SDK design", "The public API, a bad API to review, UI leaking into the core, .NET Standard 2.0, versioning."),
        ["10-migration"] = ("Migration workflow", "State machines, new stages, resuming after a crash, the lost response."),
        ["11-wpf"] = ("WPF", "Binding, the dispatcher, leaks, a reusable control."),
        ["12-maui"] = ("MAUI", "Shell navigation, collection updates, lifecycle, testable view models."),
        ["13-testing"] = ("Testing", "Repairing a flaky suite; testing retries and cancellation."),
        ["14-performance"] = ("Performance", "An N+1 planner, and a WPF gallery that eats memory."),
        ["15-code-review"] = ("Code review", "Two realistic pull requests to review."),
        ["16-system-design"] = ("System design", "SDK architecture, scale, bad networks, concurrency, versioning, Azure."),
    };

    public static string Name(string folder) => Known.TryGetValue(folder, out var c) ? c.Name : folder[3..];

    public static string Blurb(string folder) => Known.TryGetValue(folder, out var c) ? c.Blurb : "";
}
