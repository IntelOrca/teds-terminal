namespace tterm.Ansi
{
    internal enum ANSICodeType
    {
        SetCursorPosition,
        MoveToColumn,
        CursorUp,
        CursorDown,
        CursorForward,
        CursorBackward,
        SaveCursorPosition,
        RestoreCursorPosition,
        EraseDisplay,
        EraseLine,
        SetGraphicsMode,
        SetMode,
        ResetMode,
        Title,
    }
}
