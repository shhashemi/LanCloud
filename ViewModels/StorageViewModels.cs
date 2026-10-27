using LanCloud.Models;

namespace LanCloud.ViewModels;

public class ItemRow
{
    public StorageItem Item { get; set; } = null!;
    public string? OwnerName { get; set; }
    public int ShareCount { get; set; }
}

public class ItemListVm
{
    public List<ItemRow> Items { get; set; } = new();

    /// <summary>true = فایل‌های خود کاربر (امکان تغییر نام، حذف، اشتراک‌گذاری)</summary>
    public bool Owned { get; set; }

    public bool ShowOwner { get; set; }

    public string EmptyTitle { get; set; } = "اینجا خالی است";
    public string EmptyText { get; set; } = "";
}

public class FilesIndexVm
{
    public StorageItem? Folder { get; set; }
    public List<StorageItem> Crumbs { get; set; } = new();
    public ItemListVm List { get; set; } = new();
    public string? Query { get; set; }
    public string Sort { get; set; } = "name";
    public long Used { get; set; }
    public long Quota { get; set; }
}

public class ShareFolderVm
{
    public StorageItem Folder { get; set; } = null!;
    public List<StorageItem> Crumbs { get; set; } = new();
    public bool ViaShare { get; set; }
    public ItemListVm List { get; set; } = new();
}

public class ShareUserRow
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = "";
    public string Username { get; set; } = "";
    public bool Selected { get; set; }
}

public class ShareDialogVm
{
    public StorageItem Item { get; set; } = null!;
    public List<ShareUserRow> Users { get; set; } = new();
}

public class AdminUserRow
{
    public AppUser User { get; set; } = null!;
    public long Used { get; set; }
    public int FileCount { get; set; }
}
