using System.Runtime.InteropServices;
namespace Wds.Resone.Launcher;
// Win32 message loop; no timers or status polling. Runs outside the DAW process.
internal sealed class TrayIcon(Action open,Func<Task> toggle,Action exit):IDisposable
{
 private Thread? thread;private nint window;private readonly string className="Wds.Resone.Tray."+Environment.ProcessId;
 private WndProc? callback;
 public void Status(string text){if(window==0)return;var tip=text.Length>127?text[..127]:text;var data=new Notify{size=(uint)Marshal.SizeOf<Notify>(),window=window,id=1,flags=4,tip=tip,info="",title=""};Shell_NotifyIconW(1,ref data);}
 public void Start(){if(!OperatingSystem.IsWindows())return;thread=new Thread(Run){IsBackground=true,Name="Resone tray"};thread.SetApartmentState(ApartmentState.STA);thread.Start();}
 private void Run(){
  callback=Proc;var wc=new WindowClass{proc=callback,instance=GetModuleHandle(null),name=className};RegisterClassW(ref wc);
  window=CreateWindowExW(0,className,"Resone",0,0,0,0,0,0,0,wc.instance,0);
  if(window==0)return;
  var icon=new Notify{size=(uint)Marshal.SizeOf<Notify>(),window=window,id=1,flags=7,message=0x8001,icon=LoadImageW(0,Path.Combine(AppContext.BaseDirectory,"Resone.ico"),1,32,32,0x10),tip="Resone — music services",info="",title=""};Shell_NotifyIconW(0,ref icon);
  while(GetMessageW(out var msg,0,0,0)>0){TranslateMessage(ref msg);DispatchMessageW(ref msg);}
  Shell_NotifyIconW(2,ref icon);if(icon.icon!=0)DestroyIcon(icon.icon);DestroyWindow(window);window=0;UnregisterClassW(className,wc.instance);
 }
 private nint Proc(nint w,uint m,nuint wp,nint lp){
  if(m==0x8001){if(lp==0x203){Safe(open);}else if(lp==0x205){
   var menu=CreatePopupMenu();AppendMenuW(menu,0,1,"Open Resone");AppendMenuW(menu,0,2,"Toggle AI stack");AppendMenuW(menu,0,3,"Exit Resone services");GetCursorPos(out var p);SetForegroundWindow(w);var choice=TrackPopupMenu(menu,0x100|2,p.x,p.y,0,w,0);DestroyMenu(menu);
   if(choice==1)Safe(open);if(choice==2)_=Task.Run(toggle);if(choice==3)Safe(exit);
  }return 0;}
  if(m==0x10){PostQuitMessage(0);return 0;}return DefWindowProcW(w,m,wp,lp);
 }
 private static void Safe(Action action){try{action();}catch(Exception e){Console.Error.WriteLine(e.Message);}}
 public void Dispose(){if(window!=0)PostMessageW(window,0x10,0,0);thread?.Join(TimeSpan.FromSeconds(2));}
 [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint WndProc(nint w,uint m,nuint wp,nint lp);
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct WindowClass{public uint style;public WndProc proc;public int clsExtra,winExtra;public nint instance,icon,cursor,background;public string? menu;public string name;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct Notify{public uint size;public nint window;public uint id,flags,message;public nint icon;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string tip;public uint state,stateMask;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string info;public uint timeout;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)]public string title;public uint infoFlags;public Guid guid;public nint balloon;}
 [StructLayout(LayoutKind.Sequential)]private struct Point{public int x,y;}
 [StructLayout(LayoutKind.Sequential)]private struct Message{public nint hwnd;public uint message;public nuint wParam;public nint lParam;public uint time;public Point pt;public uint privateValue;}
 [DllImport("kernel32",CharSet=CharSet.Unicode)]private static extern nint GetModuleHandle(string? name);
 [DllImport("user32",CharSet=CharSet.Unicode)]private static extern ushort RegisterClassW(ref WindowClass c);
 [DllImport("user32",CharSet=CharSet.Unicode)]private static extern bool UnregisterClassW(string name,nint instance);
 [DllImport("user32",CharSet=CharSet.Unicode)]private static extern nint CreateWindowExW(uint ex,string cls,string title,uint style,int x,int y,int width,int height,nint parent,nint menu,nint instance,nint param);
 [DllImport("user32")]private static extern nint DefWindowProcW(nint w,uint m,nuint wp,nint lp);
 [DllImport("user32")]private static extern bool DestroyWindow(nint w);
 [DllImport("user32")]private static extern int GetMessageW(out Message msg,nint w,uint min,uint max);
 [DllImport("user32")]private static extern bool TranslateMessage(ref Message msg);
 [DllImport("user32")]private static extern nint DispatchMessageW(ref Message msg);
 [DllImport("user32")]private static extern bool PostMessageW(nint w,uint msg,nuint wp,nint lp);
 [DllImport("user32")]private static extern void PostQuitMessage(int code);
 [DllImport("user32",CharSet=CharSet.Unicode)]private static extern nint LoadImageW(nint instance,string path,uint type,int width,int height,uint flags);
 [DllImport("user32")]private static extern bool DestroyIcon(nint icon);
 [DllImport("shell32",CharSet=CharSet.Unicode)]private static extern bool Shell_NotifyIconW(uint action,ref Notify data);
 [DllImport("user32")]private static extern nint CreatePopupMenu();
 [DllImport("user32",CharSet=CharSet.Unicode)]private static extern bool AppendMenuW(nint menu,uint flags,nuint id,string text);
 [DllImport("user32")]private static extern bool GetCursorPos(out Point p);
 [DllImport("user32")]private static extern bool SetForegroundWindow(nint w);
 [DllImport("user32")]private static extern uint TrackPopupMenu(nint menu,uint flags,int x,int y,int reserved,nint owner,nint rect);
 [DllImport("user32")]private static extern bool DestroyMenu(nint menu);
}
