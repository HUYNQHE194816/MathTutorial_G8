using System;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// Hộp thoại chọn file CSV. Chạy trong Unity Editor và bản build Windows.
/// Nền tảng khác (Android, WebGL...): Supported = false, PickCsv trả về null.
/// </summary>
public static class FilePicker
{
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
    public static bool Supported { get { return true; } }
#else
    public static bool Supported { get { return false; } }
#endif

    /// <summary>Trả về đường dẫn file, hoặc null nếu người dùng hủy / nền tảng không hỗ trợ.</summary>
    public static string PickCsv()
    {
#if UNITY_EDITOR
        string p = UnityEditor.EditorUtility.OpenFilePanel("Chọn file CSV", "", "csv");
        return string.IsNullOrEmpty(p) ? null : p;
#elif UNITY_STANDALONE_WIN
        return WinOpen();
#else
        return null;
#endif
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    class OpenFileName
    {
        public int structSize;
        public IntPtr dlgOwner;
        public IntPtr instance;
        public string filter;
        public string customFilter;
        public int maxCustFilter;
        public int filterIndex;
        public StringBuilder file;
        public int maxFile;
        public StringBuilder fileTitle;
        public int maxFileTitle;
        public string initialDir;
        public string title;
        public int flags;
        public short fileOffset;
        public short fileExtension;
        public string defExt;
        public IntPtr custData;
        public IntPtr hook;
        public string templateName;
        public IntPtr reservedPtr;
        public int reservedInt;
        public int flagsEx;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

    [DllImport("user32.dll")]
    static extern IntPtr GetActiveWindow();

    static string WinOpen()
    {
        var ofn = new OpenFileName();
        ofn.structSize = Marshal.SizeOf(ofn);
        ofn.dlgOwner = GetActiveWindow();
        ofn.filter = "CSV (*.csv)\0*.csv\0Tất cả (*.*)\0*.*\0\0";
        ofn.filterIndex = 1;
        ofn.file = new StringBuilder(1024);
        ofn.maxFile = ofn.file.Capacity;
        ofn.fileTitle = new StringBuilder(256);
        ofn.maxFileTitle = ofn.fileTitle.Capacity;
        ofn.title = "Chọn file CSV";
        ofn.defExt = "csv";
        // OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR
        ofn.flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008;
        return GetOpenFileName(ofn) ? ofn.file.ToString() : null;
    }
#endif
}
