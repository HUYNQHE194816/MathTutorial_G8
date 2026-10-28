using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Hộp thoại chọn file CSV. Chạy trong Unity Editor và bản build Windows.
/// Nền tảng khác (Android, WebGL...): Supported = false, PickCsv trả về null.
/// Người dùng bấm hủy: PickCsv trả về null và LastError = null. Hộp thoại lỗi: LastError có nội dung.
/// </summary>
public static class FilePicker
{
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
    public static bool Supported { get { return true; } }
#else
    public static bool Supported { get { return false; } }
#endif

    /// <summary>Lý do lần PickCsv gần nhất thất bại (null nếu thành công hoặc người dùng hủy).</summary>
    public static string LastError;

    /// <summary>Trả về đường dẫn file, hoặc null nếu người dùng hủy / nền tảng không hỗ trợ / lỗi (xem LastError).</summary>
    public static string PickCsv()
    {
        LastError = null;
#if UNITY_EDITOR
        string p = UnityEditor.EditorUtility.OpenFilePanel("Chọn file CSV", "", "csv");
        return string.IsNullOrEmpty(p) ? null : p;
#elif UNITY_STANDALONE_WIN
        try { return WinOpen(); }
        catch (Exception e)
        {
            LastError = e.Message;
            Debug.LogWarning("[FilePicker] Không mở được hộp chọn file: " + e);
            return null;
        }
#else
        return null;
#endif
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    // Struct chỉ gồm kiểu blittable (IntPtr/int/short): marshal ổn định, không phụ thuộc cách Mono xử lý string/StringBuilder trong struct.
    [StructLayout(LayoutKind.Sequential)]
    struct OpenFileName
    {
        public int structSize;
        public IntPtr dlgOwner;
        public IntPtr instance;
        public IntPtr filter;
        public IntPtr customFilter;
        public int maxCustFilter;
        public int filterIndex;
        public IntPtr file;
        public int maxFile;
        public IntPtr fileTitle;
        public int maxFileTitle;
        public IntPtr initialDir;
        public IntPtr title;
        public int flags;
        public short fileOffset;
        public short fileExtension;
        public IntPtr defExt;
        public IntPtr custData;
        public IntPtr hook;
        public IntPtr templateName;
        public IntPtr reservedPtr;
        public int reservedInt;
        public int flagsEx;
    }

    [DllImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool GetOpenFileName(ref OpenFileName ofn);

    [DllImport("comdlg32.dll")]
    static extern int CommDlgExtendedError();

    [DllImport("user32.dll")]
    static extern IntPtr GetActiveWindow();

    static string WinOpen()
    {
        const int FileChars = 2048;
        IntPtr filter = IntPtr.Zero, file = IntPtr.Zero, title = IntPtr.Zero, defExt = IntPtr.Zero;
        try
        {
            filter = Marshal.StringToHGlobalUni("CSV (*.csv)\0*.csv\0Tất cả (*.*)\0*.*\0\0");
            title = Marshal.StringToHGlobalUni("Chọn file CSV");
            defExt = Marshal.StringToHGlobalUni("csv");
            file = Marshal.AllocHGlobal(FileChars * 2);
            for (int i = 0; i < FileChars; i++) Marshal.WriteInt16(file, i * 2, 0);

            var ofn = new OpenFileName();
            ofn.structSize = Marshal.SizeOf(typeof(OpenFileName));
            ofn.dlgOwner = GetActiveWindow();
            ofn.filter = filter;
            ofn.filterIndex = 1;
            ofn.file = file;
            ofn.maxFile = FileChars;
            ofn.title = title;
            ofn.defExt = defExt;
            // OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR
            ofn.flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008;

            if (GetOpenFileName(ref ofn)) return Marshal.PtrToStringUni(file);
            int err = CommDlgExtendedError();                       // 0 = người dùng hủy
            if (err != 0) LastError = "Lỗi hộp thoại Windows (mã 0x" + err.ToString("X") + ")";
            return null;
        }
        finally
        {
            if (filter != IntPtr.Zero) Marshal.FreeHGlobal(filter);
            if (title != IntPtr.Zero) Marshal.FreeHGlobal(title);
            if (defExt != IntPtr.Zero) Marshal.FreeHGlobal(defExt);
            if (file != IntPtr.Zero) Marshal.FreeHGlobal(file);
        }
    }
#endif
}
