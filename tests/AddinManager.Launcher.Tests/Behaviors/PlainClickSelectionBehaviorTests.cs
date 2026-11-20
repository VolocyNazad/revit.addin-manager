using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using System.Xml.Linq;
using AddinManager.Launcher.Abstractions;
using AddinManager.Launcher.Behaviors;
using AddinManager.Launcher.Tests.TestDoubles;

namespace AddinManager.Launcher.Tests.Behaviors;

/// <summary>
/// Обычный клик по строке списка оставляет отмеченной только её, даже если прежняя отмеченная
/// строка ушла из видимой области и её контейнер выгружен. Клик по чекбоксу, переключателю или
/// полю переименования отметки не трогает, а Ctrl и Shift сохраняют прежний выбор.
/// </summary>
public sealed class PlainClickSelectionBehaviorTests
{
    private static readonly XNamespace BehaviorsNamespace = "clr-namespace:AddinManager.Launcher.Behaviors";
    private const string EnabledAttribute = "PlainClickSelectionBehavior.Enabled";

    [Fact]
    public void Reconcile_ClearsMarkOfRowThatLeftViewport()
    {
        RunOnSta(() =>
        {
            var (listBox, rows) = CreateList();
            rows[0].IsSelected = true;
            rows[1].IsSelected = true;
            listBox.SelectedItems.Add(rows[1]);

            PlainClickSelectionBehavior.Reconcile(listBox);

            Assert.False(rows[0].IsSelected);
            Assert.True(rows[1].IsSelected);
        });
    }

    [Fact]
    public void Reconcile_KeepsEverySelectedRowMarked()
    {
        RunOnSta(() =>
        {
            var (listBox, rows) = CreateList();
            rows[0].IsSelected = true;
            rows[1].IsSelected = true;
            listBox.SelectedItems.Add(rows[0]);
            listBox.SelectedItems.Add(rows[1]);

            PlainClickSelectionBehavior.Reconcile(listBox);

            Assert.True(rows[0].IsSelected);
            Assert.True(rows[1].IsSelected);
        });
    }

    [Fact]
    public void Reconcile_DoesNotSelectRowWithoutMark()
    {
        RunOnSta(() =>
        {
            var (listBox, rows) = CreateList();
            listBox.SelectedItems.Add(rows[1]);

            PlainClickSelectionBehavior.Reconcile(listBox);

            Assert.False(rows[0].IsSelected);
            Assert.False(rows[1].IsSelected);
            Assert.Single(listBox.SelectedItems);
        });
    }

    [Theory]
    [InlineData(ModifierKeys.Control)]
    [InlineData(ModifierKeys.Shift)]
    [InlineData(ModifierKeys.Control | ModifierKeys.Shift)]
    public void ReplacesSelection_WithSelectionModifier_ReturnsFalse(ModifierKeys modifiers)
    {
        RunOnSta(() =>
        {
            var (container, content) = CreateRowContent();

            Assert.False(PlainClickSelectionBehavior.ReplacesSelection(modifiers, content, container));
        });
    }

    [Fact]
    public void ReplacesSelection_OnRowContent_ReturnsTrue()
    {
        RunOnSta(() =>
        {
            var (container, content) = CreateRowContent();

            Assert.True(PlainClickSelectionBehavior.ReplacesSelection(ModifierKeys.None, content, container));
        });
    }

    [Fact]
    public void ReplacesSelection_OnRowBackground_ReturnsTrue()
    {
        RunOnSta(() =>
        {
            var (container, _) = CreateRowContent();

            Assert.True(PlainClickSelectionBehavior.ReplacesSelection(ModifierKeys.None, container, container));
        });
    }

    [Fact]
    public void ReplacesSelection_OnSelectionCheckBox_ReturnsFalse()
    {
        RunOnSta(() =>
        {
            var (container, content) = CreateRowContent();
            var checkBox = new CheckBox();
            ((StackPanel)container.Child).Children.Add(checkBox);

            Assert.False(PlainClickSelectionBehavior.ReplacesSelection(ModifierKeys.None, checkBox, container));
            Assert.True(PlainClickSelectionBehavior.ReplacesSelection(ModifierKeys.None, content, container));
        });
    }

    [Fact]
    public void ReplacesSelection_OnRenameBox_ReturnsFalse()
    {
        RunOnSta(() =>
        {
            var (container, _) = CreateRowContent();
            var renameBox = new TextBox();
            ((StackPanel)container.Child).Children.Add(renameBox);

            Assert.False(PlainClickSelectionBehavior.ReplacesSelection(ModifierKeys.None, renameBox, container));
        });
    }

    [Fact]
    public void ReplacesSelection_OutsideAnyRow_ReturnsFalse()
    {
        Assert.False(PlainClickSelectionBehavior.ReplacesSelection(ModifierKeys.None, null, null));
    }

    [Fact]
    public void Reconcile_ClearsMarkInGroupedList()
    {
        RunOnSta(() =>
        {
            var rows = new List<TestRow>
            {
                new() { Group = "2025" },
                new() { Group = "2024" },
            };
            var view = new ListCollectionView(rows);
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(TestRow.Group)));
            var listBox = new ListBox
            {
                ItemsSource = view,
                SelectionMode = SelectionMode.Extended,
            };
            rows[0].IsSelected = true;
            rows[1].IsSelected = true;
            listBox.SelectedItems.Add(rows[1]);

            PlainClickSelectionBehavior.Reconcile(listBox);

            Assert.False(rows[0].IsSelected);
            Assert.True(rows[1].IsSelected);
        });
    }

    [Theory]
    [InlineData("ListView.xaml")]
    [InlineData("EntriesView.xaml")]
    public void View_EnablesBehaviorOnItsList(string fileName)
    {
        var path = Path.Combine(RepositoryRoot.Find(), "src", "AddinManager.Launcher", "Views", fileName);
        var listBoxes = XDocument.Load(path)
            .Descendants()
            .Where(element => element.Name.LocalName == "ListBox")
            .ToList();

        Assert.NotEmpty(listBoxes);
        Assert.All(listBoxes, listBox =>
            Assert.Equal("True", (string?)listBox.Attribute(BehaviorsNamespace + EnabledAttribute)));
    }

    private static (ListBox ListBox, TestRow[] Rows) CreateList()
    {
        var rows = new[] { new TestRow(), new TestRow() };
        var listBox = new ListBox
        {
            ItemsSource = rows,
            SelectionMode = SelectionMode.Extended,
        };

        PlainClickSelectionBehavior.SetEnabled(listBox, true);
        return (listBox, rows);
    }

    private static (Border Container, TextBlock Content) CreateRowContent()
    {
        var container = new Border();
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        var content = new TextBlock { Text = "A.addin" };
        panel.Children.Add(content);
        container.Child = panel;
        return (container, content);
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            throw new InvalidOperationException("Проверка WPF упала на STA-потоке.", failure);
    }

    private sealed class TestRow : IBulkSelectableRow
    {
        public string Group { get; init; } = "2025";

        public bool IsSelected { get; set; }
    }
}
