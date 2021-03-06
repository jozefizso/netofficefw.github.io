﻿using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace NetOffice.PowerPointApi.Tools.Utils
{
    /// <summary>
    /// Application related utils
    /// </summary>
    public class ApplicationUtils
    {
        #region Imports

        /// <summary>
        /// Application VTable interface
        /// </summary>
        [DefaultMember("Name"), Guid("91493442-5A91-11CF-8700-00AA0060263B"), TypeLibType(4288)]
        [ComImport]
        public interface VTableApplication
        {
            /// <summary>
            /// Main window handle from application window
            /// </summary>
            [DispId(2031)]
            int HWND
            {
                [DispId(2031), TypeLibFunc(1)]
                [MethodImpl(4096)]
                get;
            }
        }

        [DllImport("User32")]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("User32")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildCallback lpEnumFunc, ref int lParam);
     
        [DllImport("Oleacc.dll")]
        private static extern int AccessibleObjectFromWindow(int hwnd, uint dwObjectID, byte[] riid, [MarshalAs(UnmanagedType.IDispatch)]ref object ptr);

        private delegate bool EnumChildCallback(IntPtr hwnd, ref int lParam);

        #endregion

        #region Fields

        private static uint objid_NATIVEOM = 0xFFFFFFF0;
        private static Guid _dispatch = new Guid("00020400-0000-0000-C000-000000000046");
        private static Guid _unknown = new Guid("00000000-0000-0000-C000-000000000046");

        private CommonUtils _owner;
        private int _hwnd = 0;

        #endregion

        #region Ctor

        /// <summary>
        /// Creates an instance of the class
        /// </summary>
        /// <param name="owner">owner instance</param>
        protected internal ApplicationUtils(CommonUtils owner)
        {
            if (null == owner)
                throw new ArgumentNullException("owner");
            _owner = owner;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Host application main window handle
        /// </summary>
        public int HWND
        {
            get
            {
                if (0 == _hwnd)
                    _hwnd = TryGetHostApplicationWindowHandle();
                return _hwnd;
            }
        }

        #endregion

        private bool EnumChildProc(IntPtr hwnd, ref int lParam)
        {
            StringBuilder windowClass = new StringBuilder(128);
            GetClassName(hwnd, windowClass, 128);
            if (windowClass.ToString() == "paneClassDC")
                lParam = (int)hwnd;
            return true;
        }

        #region Methods

        private int TryGetHostApplicationWindowHandle()
        {
            int result = TryGetHostApplicationWindowHandleFromVTable();
            if(0 == result)
                result = TryGetHostApplicationWindowHandleFromDesktop();
            return result;
        }

        private int TryGetHostApplicationWindowHandleFromVTable()        
        {
            try
            {
                VTableApplication test = _owner.PowerPointApplication.UnderlyingObject as VTableApplication;
                return null != test ? test.HWND : 0;
            }
            catch (Exception exception)
            {
                NetOffice.Core.Default.Console.WriteException(exception);
                return 0;
            }
        }

        private int TryGetHostApplicationWindowHandleFromDesktop()
        {
            try
            {
                int result = 0;
                NetOffice.Tools.WndUtils.WindowEnumerator enumerator = new NetOffice.Tools.WndUtils.WindowEnumerator("PP", "FrameClass");
                IntPtr[] handles = enumerator.EnumerateWindows(2000);

                foreach (IntPtr item in handles)
                {
                    object proxyApplication = GetAccessibleObject(item);
                    if (null != proxyApplication)
                    {
                        try
                        {
                            bool equals = Equal(_owner.PowerPointApplication.UnderlyingObject, proxyApplication);
                            if (equals)
                                result = (int)item;
                            break;
                        }
                        catch
                        {
                            throw;
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(proxyApplication);
                        }
                    }                    
                }

                return result;
            }
            catch (Exception exception)
            {
                NetOffice.Core.Default.Console.WriteException(exception);
                return 0;
            }
        }

        private object GetAccessibleObject(IntPtr hwnd)
        {
            if (hwnd != IntPtr.Zero)
            {
                int hWndChild = 0;

                EnumChildCallback cb = new EnumChildCallback(EnumChildProc);
                EnumChildWindows(hwnd, cb, ref hWndChild);

                if (hWndChild != 0)
                {
                    object ptr = null;
                    int hr = AccessibleObjectFromWindow(hWndChild, objid_NATIVEOM, _dispatch.ToByteArray(), ref ptr);
                    if (hr >= 0)
                        return NetOffice.Core.Default.Invoker.PropertyGet(ptr, "Application");
                }
            }

            return null;
        }

        private static int GetVBEMainWindowHandle(object applicationProxy)
        {
            PowerPointApi.Application app = null;
            try
            {
                Core core = new Core();
                app = new Application(core, null, applicationProxy);
                int result = app.VBE.MainWindow.HWnd;
                return result;
            }
            catch
            {
                throw;
            }
            finally
            {
                if (null != app)
                    app.DisposeChildInstances();
            }
        }

        private bool Equal(object applicationProxyA, object applicationProxyB)
        {
            try
            {
                int hwndA = GetVBEMainWindowHandle(applicationProxyA);
                int hwndB = GetVBEMainWindowHandle(applicationProxyB);
                return hwndA == hwndB;        
            }
            catch (Exception)
            {
                throw;
            }
        }

        #endregion
    }
}
