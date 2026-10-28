using System;
using Avalonia.Controls;
using Avalonia.Input;
using CodeTrace.Student.Views;

namespace CodeTrace.Student;

public partial class MainWindow : Window
{
    private bool _allowClose;
    private ExamSessionView? _activeSession;   // non-null only while an exam is running

    public MainWindow()
    {
        InitializeComponent();
        ShowJoin();

        Closing += OnClosing;

        // Teacher escape hatch: Ctrl+Shift+Alt+F12 (change to a combo only you know)
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.F12 &&
                e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt))
            {
                CloseApp();
            }
        };
    }

    private void ShowJoin()
    {
        _activeSession = null;
        var join = new JoinExamView();
        join.ExamJoined += (_, session) => ShowExamSession(session);
        MainContent.Content = join;
    }

    private void ShowExamSession((Models.Exam Exam, Models.StudentSession Session, string StudentName) info)
    {
        var session = new ExamSessionView(info.Exam, info.Session, info.StudentName);

        // Covers both the Submit button and the timer reaching 0:
        // ExamSessionView raises Submitted after a successful submit.
        session.Submitted += (_, _) => CloseApp();

        _activeSession = session;
        MainContent.Content = session;
    }

    public void CloseApp()
    {
        _allowClose = true;
        Close();
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_allowClose) return;

        e.Cancel = true;   // decide below

        if (_activeSession != null)
        {
            // Alt+F4 etc. during an exam: end it for this student, then close
            try { await _activeSession.EndExamAsync("Student exited"); }
            catch { /* close even if the server is unreachable */ }

            CloseApp();
        }
        // On the login/join screen, closing is ignored
    }
}