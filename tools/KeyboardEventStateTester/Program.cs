using System.Collections.Generic;

var tester = new KeyboardStateTester(singleKeyDelayMs: 120);

RunCase(
    "単押しは遅延後に1回だけ発火",
    tester,
    new[]
    {
        E(0, "P", "SW01"),
        E(130, "R", "SW01"),
    },
    expectedActions: new[] { "single:SW01@120" });

RunCase(
    "2キー同時押しはコンボのみ発火（単押しキャンセル）",
    tester,
    new[]
    {
        E(0, "P", "SW01"),
        E(50, "P", "SW02"),
        E(90, "R", "SW01"),
        E(120, "R", "SW02"),
    },
    expectedActions: new[] { "combo:SW01+SW02@50" });

RunCase(
    "エンコーダ疑似タップ(P/R)は張り付きなく単発発火",
    tester,
    new[]
    {
        E(0, "P", "SW36"),
        E(1, "R", "SW36"),
        E(200, "P", "SW36"),
        E(201, "R", "SW36"),
    },
    expectedActions: new[] { "single:SW36@120", "single:SW36@320" });

Console.WriteLine("All test cases passed.");

static InputEvent E(int timeMs, string state, string switchName)
    => new(timeMs, state, switchName);

static void RunCase(
    string title,
    KeyboardStateTester tester,
    IEnumerable<InputEvent> events,
    IEnumerable<string> expectedActions)
{
    tester.Reset();
    foreach (var ev in events)
        tester.Handle(ev);

    var actual = tester.DrainActions();
    var expected = expectedActions.ToArray();
    if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
    {
        Console.WriteLine($"[FAILED] {title}");
        Console.WriteLine($"  expected: {string.Join(", ", expected)}");
        Console.WriteLine($"  actual  : {string.Join(", ", actual)}");
        Environment.Exit(1);
    }

    Console.WriteLine($"[OK] {title}");
}

internal sealed record InputEvent(int TimeMs, string State, string SwitchName);

internal sealed class KeyboardStateTester
{
    private readonly int singleKeyDelayMs;
    private readonly HashSet<string> pressed = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> consumed = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> pendingSingles = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> actions = new();
    private int nowMs;

    public KeyboardStateTester(int singleKeyDelayMs)
    {
        this.singleKeyDelayMs = singleKeyDelayMs;
    }

    public void Reset()
    {
        pressed.Clear();
        consumed.Clear();
        pendingSingles.Clear();
        actions.Clear();
        nowMs = 0;
    }

    public void Handle(InputEvent ev)
    {
        AdvanceTo(ev.TimeMs);

        if (string.Equals(ev.State, "P", StringComparison.OrdinalIgnoreCase))
            OnPressed(ev.SwitchName);
        else
            OnReleased(ev.SwitchName);
    }

    public string[] DrainActions()
    {
        AdvanceTo(nowMs + 500);
        return actions.ToArray();
    }

    private void AdvanceTo(int targetMs)
    {
        if (targetMs < nowMs)
            throw new InvalidOperationException("Input event time must be monotonic.");

        nowMs = targetMs;
        var due = pendingSingles
            .Where(p => p.Value <= nowMs)
            .OrderBy(p => p.Value)
            .ToArray();

        foreach (var item in due)
        {
            pendingSingles.Remove(item.Key);
            if (!consumed.Contains(item.Key))
                actions.Add($"single:{item.Key}@{item.Value}");
        }
    }

    private void OnPressed(string switchName)
    {
        pressed.Add(switchName);

        if (pressed.Count == 1)
        {
            pendingSingles[switchName] = nowMs + singleKeyDelayMs;
            return;
        }

        var comboKey = string.Join("+", pressed.OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
        actions.Add($"combo:{comboKey}@{nowMs}");

        foreach (var sw in pressed)
        {
            consumed.Add(sw);
            pendingSingles.Remove(sw);
        }
    }

    private void OnReleased(string switchName)
    {
        pressed.Remove(switchName);
        consumed.Remove(switchName);
    }
}
