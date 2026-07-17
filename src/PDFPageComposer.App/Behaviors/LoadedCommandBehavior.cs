using System.Windows;
using System.Windows.Input;

namespace PDFPageComposer.App.Behaviors;

public static class LoadedCommandBehavior
{
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached(
            "Command",
            typeof(ICommand),
            typeof(LoadedCommandBehavior),
            new PropertyMetadata(null, OnCommandChanged));

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.RegisterAttached(
            "CommandParameter",
            typeof(object),
            typeof(LoadedCommandBehavior),
            new PropertyMetadata(null));

    public static readonly DependencyProperty UnloadedCommandProperty =
        DependencyProperty.RegisterAttached(
            "UnloadedCommand",
            typeof(ICommand),
            typeof(LoadedCommandBehavior),
            new PropertyMetadata(null, OnUnloadedCommandChanged));

    public static void SetCommand(DependencyObject element, ICommand? value)
    {
        element.SetValue(CommandProperty, value);
    }

    public static ICommand? GetCommand(DependencyObject element)
    {
        return (ICommand?)element.GetValue(CommandProperty);
    }

    public static void SetCommandParameter(DependencyObject element, object? value)
    {
        element.SetValue(CommandParameterProperty, value);
    }

    public static object? GetCommandParameter(DependencyObject element)
    {
        return element.GetValue(CommandParameterProperty);
    }

    public static void SetUnloadedCommand(DependencyObject element, ICommand? value)
    {
        element.SetValue(UnloadedCommandProperty, value);
    }

    public static ICommand? GetUnloadedCommand(DependencyObject element)
    {
        return (ICommand?)element.GetValue(UnloadedCommandProperty);
    }

    private static void OnCommandChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not FrameworkElement element)
        {
            return;
        }

        element.Loaded -= OnLoaded;
        if (e.NewValue is ICommand)
        {
            element.Loaded += OnLoaded;
        }
    }

    private static void OnUnloadedCommandChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not FrameworkElement element)
        {
            return;
        }

        element.Unloaded -= OnUnloaded;
        if (e.NewValue is ICommand)
        {
            element.Unloaded += OnUnloaded;
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DependencyObject element)
        {
            return;
        }

        var command = GetCommand(element);
        var parameter = GetCommandParameter(element);
        if (command?.CanExecute(parameter) == true)
        {
            command.Execute(parameter);
        }
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DependencyObject element)
        {
            return;
        }

        var command = GetUnloadedCommand(element);
        var parameter = GetCommandParameter(element);
        if (command?.CanExecute(parameter) == true)
        {
            command.Execute(parameter);
        }
    }
}
