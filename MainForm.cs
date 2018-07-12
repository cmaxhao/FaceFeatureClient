using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Threading;
using System.Windows.Forms;
using System.IO;
using System.Data.SqlClient;

namespace DF_FaceTracking.cs
{
    public partial class MainForm : Form
    {
        public enum Label
        {
            StatusLabel,
            AlertsLabel
        };

        public PXCMSession Session;
        public FacialExpression Expression;
        public volatile bool RegisterThisID = false;
        public volatile bool Stopped = false;
        public readonly Dictionary<PXCMFaceConfiguration.TrackingModeType, string> FaceModesMap =
           new Dictionary<PXCMFaceConfiguration.TrackingModeType, string>()
           {
                { PXCMFaceConfiguration.TrackingModeType.FACE_MODE_COLOR_PLUS_DEPTH , "3D Tracking" },
                { PXCMFaceConfiguration.TrackingModeType.FACE_MODE_COLOR, "2D Tracking" },
                { PXCMFaceConfiguration.TrackingModeType.FACE_MODE_IR, "IR Tracking" },
                { PXCMFaceConfiguration.TrackingModeType.FACE_MODE_COLOR_STILL, "2D Still" }
           };

        private readonly object m_bitmapLock = new object();
        private readonly FaceTextOrganizer m_faceTextOrganizer;
        private Dictionary<String, ModuleSettings> m_moduleSettings;
        private Bitmap m_bitmap;
        private string m_filename;
        private Tuple<PXCMImage.ImageInfo, PXCMRangeF32> m_selectedColorResolution;
        private volatile bool m_closing;
        private static ToolStripMenuItem m_deviceMenuItem;
        private static ToolStripMenuItem m_moduleMenuItem;
        private const int LandmarkAlignment = -3;
        private const int DefaultNumberOfFaces = 4;
        private string sessionID;
        public MainForm(PXCMSession session)
        {
            InitializeComponent();

            m_faceTextOrganizer = new FaceTextOrganizer();
            m_deviceMenuItem = new ToolStripMenuItem("Device");
            m_moduleMenuItem = new ToolStripMenuItem("Module");
            Session = session;

            Expression = new FacialExpression();

            CreateResolutionMap();
            PopulateDeviceMenu();
            PopulateModuleMenu();
            PopulateProfileMenu();

            InitializeUserSettings();
            DisableUnsupportedAlgos();

            FormClosing += MainForm_FormClosing;
            Panel2.Paint += Panel_Paint;

            Stream fs_less = new FileStream(string.Format("{0}{1}",Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"\FaceDateLess.txt"), FileMode.Append);
            sw_less = new StreamWriter(fs_less);
            sw_less.BaseStream.Seek(0, SeekOrigin.End);
            //新增保存特征点功能
            Stream fileFeature = new FileStream(string.Format("{0}{1}", Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"\FaceFeature.txt"), FileMode.Append);
            sw_feature = new StreamWriter(fileFeature);
            sw_feature.BaseStream.Seek(0, SeekOrigin.End);
            //创建存储视频的文件夹
            sessionID = Guid.NewGuid().ToString();
            string videoPath = Environment.CurrentDirectory + "\\data\\" + sessionID;
            try
            {
                Directory.CreateDirectory(videoPath);
            }
            catch (Exception ex) {

            }
        }

        private void InitializeUserSettings()
        {
            m_moduleSettings = new Dictionary<String, ModuleSettings>();

            //根据摄像头版本设置代码
            var deviceName = GetDeviceName();
            //realsense摄像头对应的设置
            if (deviceName == "IVcam")
            {
                m_moduleSettings["Detection"] = new ModuleSettings { IsEnabled = true, NumberOfFaces = 4 };
                m_moduleSettings["Landmarks"] = new ModuleSettings { IsEnabled = true, NumberOfFaces = 4 };
                m_moduleSettings["Pose"] = new ModuleSettings { IsEnabled = true, NumberOfFaces = 4 };
                m_moduleSettings["Expressions"] = new ModuleSettings { IsEnabled = true, NumberOfFaces = 4 };
                m_moduleSettings["Pulse"] = new ModuleSettings { IsEnabled = false, NumberOfFaces = 4 };
            }
        }

        private string GetDeviceName()
        {
            var deviceMenuString = GetCheckedDevice();

            if (deviceMenuString == null)
                return "InvalidCamera";

            if (deviceMenuString.Contains("R200") || deviceMenuString.Contains("DS4"))
            {
                return "DS4";
            }

            return "IVcam";
        }

        private void DisableUnsupportedAlgos()
        {
            string deviceMenuString = GetCheckedDevice();

            if (deviceMenuString != null && (deviceMenuString.Contains("R200") || deviceMenuString.Contains("DS4")))
            {
            }
        }

        public Dictionary<string, PXCMCapture.DeviceInfo> Devices { get; set; }
        public Dictionary<string, IEnumerable<Tuple<PXCMImage.ImageInfo, PXCMRangeF32>>> ColorResolutions { get; set; }

        private readonly List<Tuple<int, int>> SupportedColorResolutions = new List<Tuple<int, int>>
        {
            Tuple.Create(1920, 1080),
            Tuple.Create(1280, 720),
            Tuple.Create(960, 540),
            Tuple.Create(640, 480),
            Tuple.Create(640, 360),
        };
        
        //最大识别脸的数量默认值全部设置为4
        public string GetFileName()
        {
            return m_filename;
        }
        public bool IsRecognitionChecked()
        {
            return false; 
        }

        private void CreateResolutionMap()
        {
            ColorResolutions = new Dictionary<string, IEnumerable<Tuple<PXCMImage.ImageInfo, PXCMRangeF32>>>();
            var desc = new PXCMSession.ImplDesc
            {
                group = PXCMSession.ImplGroup.IMPL_GROUP_SENSOR,
                subgroup = PXCMSession.ImplSubgroup.IMPL_SUBGROUP_VIDEO_CAPTURE
            };

            for (int i = 0; ; i++)
            {
                PXCMSession.ImplDesc desc1;
                if (Session.QueryImpl(desc, i, out desc1) < pxcmStatus.PXCM_STATUS_NO_ERROR) break;

                PXCMCapture capture;
                if (Session.CreateImpl(desc1, out capture) < pxcmStatus.PXCM_STATUS_NO_ERROR) continue;

                for (int j = 0; ; j++)
                {
                    PXCMCapture.DeviceInfo info;
                    if (capture.QueryDeviceInfo(j, out info) < pxcmStatus.PXCM_STATUS_NO_ERROR) break;

                    PXCMCapture.Device device = capture.CreateDevice(j);
                    if (device == null)
                    {
                        throw new Exception("PXCMCapture.Device null");
                    }
                    var deviceResolutions = new List<Tuple<PXCMImage.ImageInfo, PXCMRangeF32>>();

                    for (int k = 0; k < device.QueryStreamProfileSetNum(PXCMCapture.StreamType.STREAM_TYPE_COLOR); k++)
                    {
                        PXCMCapture.Device.StreamProfileSet profileSet;
                        device.QueryStreamProfileSet(PXCMCapture.StreamType.STREAM_TYPE_COLOR, k, out profileSet);
                        PXCMCapture.DeviceInfo dinfo;
                        device.QueryDeviceInfo(out dinfo);

                        var currentRes = new Tuple<PXCMImage.ImageInfo, PXCMRangeF32>(profileSet.color.imageInfo, profileSet.color.frameRate);

                        if (IsProfileSupported(profileSet, dinfo))
                            continue;

                        if (SupportedColorResolutions.Contains(new Tuple<int, int>(currentRes.Item1.width, currentRes.Item1.height)))
                            deviceResolutions.Add(currentRes);

                    }

                    try
                    {
                        ColorResolutions.Add(info.name, deviceResolutions);
                    }
                    catch (Exception e)
                    {
                    }
                    device.Dispose();
                }

                capture.Dispose();
            }
        }

        private static bool IsProfileSupported(PXCMCapture.Device.StreamProfileSet profileSet, PXCMCapture.DeviceInfo dinfo)
        {
            return 
                (profileSet.color.frameRate.min < 30) ||
                (dinfo != null && dinfo.model == PXCMCapture.DeviceModel.DEVICE_MODEL_DS4 &&
                (profileSet.color.imageInfo.width == 1920 || profileSet.color.frameRate.min > 30 || profileSet.color.imageInfo.format == PXCMImage.PixelFormat.PIXEL_FORMAT_YUY2)) || 
                (profileSet.color.options == PXCMCapture.Device.StreamOption.STREAM_OPTION_UNRECTIFIED);
        }

        public void PopulateDeviceMenu()
        {
            Devices = new Dictionary<string, PXCMCapture.DeviceInfo>();
            var desc = new PXCMSession.ImplDesc
            {
                group = PXCMSession.ImplGroup.IMPL_GROUP_SENSOR,
                subgroup = PXCMSession.ImplSubgroup.IMPL_SUBGROUP_VIDEO_CAPTURE
            };

            for (int i = 0; ; i++)
            {
                PXCMSession.ImplDesc desc1;
                if (Session.QueryImpl(desc, i, out desc1) < pxcmStatus.PXCM_STATUS_NO_ERROR) break;

                PXCMCapture capture;
                if (Session.CreateImpl(desc1, out capture) < pxcmStatus.PXCM_STATUS_NO_ERROR) continue;

                for (int j = 0; ; j++)
                {
                    PXCMCapture.DeviceInfo dinfo;
                    if (capture.QueryDeviceInfo(j, out dinfo) < pxcmStatus.PXCM_STATUS_NO_ERROR) break;
                    string name = dinfo.name;
                    if (Devices.ContainsKey(dinfo.name))
                        name += j;
                    Devices.Add(name, dinfo);
                    var sm1 = new ToolStripMenuItem(dinfo.name, null, Device_Item_Click);
                    m_deviceMenuItem.DropDownItems.Add(sm1);
                    sm1.Click += (sender, eventArgs) =>
                    {
                        DisableUnsupportedAlgos();
                    };
                }

                capture.Dispose();
            }

            if (m_deviceMenuItem.DropDownItems.Count > 0)
            {
                ((ToolStripMenuItem)m_deviceMenuItem.DropDownItems[0]).Checked = true;
                PopulateColorResolutionMenu(m_deviceMenuItem.DropDownItems[0].ToString());
            }

            try
            {
                MainMenu.Items.RemoveAt(0);
            }
            catch (NotSupportedException)
            {
                m_deviceMenuItem.Dispose();
                throw;
            }
            MainMenu.Items.Insert(0, m_deviceMenuItem);
        }

        public void PopulateColorResolutionMenu(string deviceName)
        {
            bool foundDefaultResolution = false;
            var sm = new ToolStripMenuItem("Color");
            foreach (var resolution in ColorResolutions[deviceName])
            {
                var resText = PixelFormat2String(resolution.Item1.format) + " " + resolution.Item1.width + "x"
                              + resolution.Item1.height + " " + resolution.Item2.max + " fps";
                var sm1 = new ToolStripMenuItem(resText, null);
                var selectedResolution = resolution;
                sm1.Click += (sender, eventArgs) =>
                {
                    m_selectedColorResolution = selectedResolution;
                    ColorResolution_Item_Click(sender);
                };

                sm.DropDownItems.Add(sm1);

                int width = selectedResolution.Item1.width;
                int height = selectedResolution.Item1.height;
                PXCMImage.PixelFormat format = selectedResolution.Item1.format;
                float fps = selectedResolution.Item2.min;

                if (DefaultCameraConfig.IsDefaultDeviceConfig(deviceName, width, height, format, fps))
                {
                    foundDefaultResolution = true;
                    sm1.Checked = true;
                    sm1.PerformClick();
                }
            }

            if (!foundDefaultResolution && sm.DropDownItems.Count > 0)
            {
                ((ToolStripMenuItem)sm.DropDownItems[0]).Checked = true;
                ((ToolStripMenuItem)sm.DropDownItems[0]).PerformClick();
            }

            try
            {
                MainMenu.Items.RemoveAt(1);
            }
            catch (NotSupportedException)
            {
                sm.Dispose();
                throw;
            }
            MainMenu.Items.Insert(1, sm);
        }

        public class DefaultCameraConfig
        {
            public static bool IsDefaultDeviceConfig(string deviceName, int width, int height, PXCMImage.PixelFormat format, float fps)
            {
                if (deviceName.Contains("R200"))
                {
                    return width == DefaultDs4Width && height == DefaultDs4Height && format == DefaultDs4PixelFormat && fps == DefaultDs4Fps;
                }

                if (deviceName.Contains("F200") || deviceName.Contains("SR300"))
                {
                    return width == DefaultIvcamWidth && height == DefaultIvcamHeight && format == DefaultIvcamPixelFormat && fps == DefaultIvcamFps;
                }

                return false;
            }

            private static readonly int DefaultDs4Width = 640;
            private static readonly int DefaultDs4Height = 480;
            private static readonly PXCMImage.PixelFormat DefaultDs4PixelFormat = PXCMImage.PixelFormat.PIXEL_FORMAT_RGB32;
            private static readonly float DefaultDs4Fps = 30f;

            private static readonly int DefaultIvcamWidth = 640;
            private static readonly int DefaultIvcamHeight = 360;
            private static readonly PXCMImage.PixelFormat DefaultIvcamPixelFormat = PXCMImage.PixelFormat.PIXEL_FORMAT_YUY2;
            private static readonly float DefaultIvcamFps = 30f;
        }



        private void PopulateModuleMenu()
        {
            var desc = new PXCMSession.ImplDesc();
            desc.cuids[0] = PXCMFaceModule.CUID;

            for (int i = 0; ; i++)
            {
                PXCMSession.ImplDesc desc1;
                if (Session.QueryImpl(desc, i, out desc1) < pxcmStatus.PXCM_STATUS_NO_ERROR) break;
                var mm1 = new ToolStripMenuItem(desc1.friendlyName, null, Module_Item_Click);
                m_moduleMenuItem.DropDownItems.Add(mm1);
            }
            if (m_moduleMenuItem.DropDownItems.Count > 0)
                ((ToolStripMenuItem)m_moduleMenuItem.DropDownItems[0]).Checked = true;
            try
            {
                MainMenu.Items.RemoveAt(2);
            }
            catch (NotSupportedException)
            {
                m_moduleMenuItem.Dispose();
                throw;
            }
            MainMenu.Items.Insert(2, m_moduleMenuItem);

        }

        private void PopulateProfileMenu()
        {
            var pm = new ToolStripMenuItem("Profile");

            foreach (
                var trackingMode in FaceModesMap.Keys)
            {
                var pm1 = new ToolStripMenuItem(FaceModesMap[trackingMode], null, Profile_Item_Click);
                pm.DropDownItems.Add(pm1);

                if (trackingMode == PXCMFaceConfiguration.TrackingModeType.FACE_MODE_COLOR_PLUS_DEPTH) //3d = default
                {
                    pm1.Checked = true;
                }
            }
            try
            {
                MainMenu.Items.RemoveAt(3);
            }
            catch (NotSupportedException)
            {
                pm.Dispose();
                throw;
            }
            MainMenu.Items.Insert(3, pm);
        }

        private static string PixelFormat2String(PXCMImage.PixelFormat format)
        {
            switch (format)
            {
                case PXCMImage.PixelFormat.PIXEL_FORMAT_YUY2:
                    return "YUY2";
                case PXCMImage.PixelFormat.PIXEL_FORMAT_RGB32:
                    return "RGB32";
                case PXCMImage.PixelFormat.PIXEL_FORMAT_RGB24:
                    return "RGB24";
            }
            return "NA";
        }

        private void RadioCheck(object sender, string name)
        {
            foreach (ToolStripMenuItem m in MainMenu.Items)
            {
                if (!m.Text.Equals(name)) continue;
                foreach (ToolStripMenuItem e1 in m.DropDownItems)
                {
                    e1.Checked = (sender == e1);
                }
            }
        }

        private void ColorResolution_Item_Click(object sender)
        {
            RadioCheck(sender, "Color");
        }

        private void Device_Item_Click(object sender, EventArgs e)
        {
            PopulateColorResolutionMenu(sender.ToString());
            RadioCheck(sender, "Device");
        }

        private void Module_Item_Click(object sender, EventArgs e)
        {
            RadioCheck(sender, "Module");
            PopulateProfileMenu();
        }

        private void Profile_Item_Click(object sender, EventArgs e)
        {
            RadioCheck(sender, "Profile");
        }

        private void Start_Click(object sender, EventArgs e)
        {
            //zz
            FaceTracking.m_frame = 0;

            SaveUserSettings();
            MainMenu.Enabled = false;
            Start.Enabled = false;
            Stop.Enabled = true;

            Stopped = false;
            //此处开始做人脸识别跟踪
            var thread = new Thread(DoTracking);
            thread.Start();
        }

        private void DoTracking()
        {
            var ft = new FaceTracking(this);
            ft.SimplePipeline();

            Invoke(new DoTrackingCompleted(() =>
            {
                DisableUnsupportedAlgos();

                MainMenu.Enabled = true;
                Start.Enabled = true;
                Stop.Enabled = false;
                if (m_closing) Close();
            }));
        }

        private void Stop_Click(object sender, EventArgs e)
        {
            Stopped = true;
            isStart = false;
            
        }

        private void SaveUserSettings()
        {
            m_moduleSettings["Detection"].IsEnabled = true;
            m_moduleSettings["Detection"].NumberOfFaces = 4;

            m_moduleSettings["Landmarks"].IsEnabled = true;
            m_moduleSettings["Landmarks"].NumberOfFaces = 4;

            m_moduleSettings["Pose"].IsEnabled = true;
            m_moduleSettings["Pose"].NumberOfFaces = 4;

            m_moduleSettings["Expressions"].IsEnabled = true;
            m_moduleSettings["Expressions"].NumberOfFaces =4;

            m_moduleSettings["Pulse"].IsEnabled = false;
            m_moduleSettings["Pulse"].NumberOfFaces = 4;

        }

        public string GetCheckedDevice()
        {
            return (from ToolStripMenuItem m in MainMenu.Items
                    where m.Text.Equals("Device")
                    from ToolStripMenuItem e in m.DropDownItems
                    where e.Checked
                    select e.Text).FirstOrDefault();
        }

        public PXCMCapture.DeviceInfo GetCheckedDeviceInfo()
        {
            foreach (ToolStripMenuItem m in MainMenu.Items)
            {
                if (!m.Text.Equals("Device")) continue;
                for (int i = 0; i < m.DropDownItems.Count; i++)
                {
                    if (((ToolStripMenuItem)m.DropDownItems[i]).Checked)
                        return Devices.ElementAt(i).Value;
                }
            }
            return new PXCMCapture.DeviceInfo();
        }

        public Tuple<PXCMImage.ImageInfo, PXCMRangeF32> GetCheckedColorResolution()
        {
            return m_selectedColorResolution;
        }

        public string GetCheckedModule()
        {
            return (from ToolStripMenuItem m in MainMenu.Items
                    where m.Text.Equals("Module")
                    from ToolStripMenuItem e in m.DropDownItems
                    where e.Checked
                    select e.Text).FirstOrDefault();
        }

        public string GetCheckedProfile()
        {
            foreach (var m in from ToolStripMenuItem m in MainMenu.Items where m.Text.Equals("Profile") select m)
            {
                for (var i = 0; i < m.DropDownItems.Count; i++)
                {
                    if (((ToolStripMenuItem)m.DropDownItems[i]).Checked)
                        return m.DropDownItems[i].Text;
                }
            }
            return "";
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Stopped = true;
            e.Cancel = Stop.Enabled;
            m_closing = true;
        }

        public void UpdateStatus(string status, Label label)
        {
            if (label == Label.StatusLabel)
                Status2.Invoke(new UpdateStatusDelegate(delegate(string s) { StatusLabel.Text = s; }),
                    new object[] { status });

            if (label == Label.AlertsLabel)
                Status2.Invoke(new UpdateStatusDelegate(delegate(string s) { AlertsLabel.Text = s; }),
                    new object[] { status });
        }

        private void Panel_Paint(object sender, PaintEventArgs e)
        {
            lock (m_bitmapLock)
            {
                if (m_bitmap == null) return;

                    //是否显示视频图像
                    e.Graphics.DrawImage(m_bitmap, Panel2.ClientRectangle);

 
            }
        }

        public void UpdatePanel()
        {
            Panel2.Invoke(new UpdatePanelDelegate(() => Panel2.Invalidate()));
        }

        public void DrawBitmap(Bitmap picture)
        {
            lock (m_bitmapLock)
            {
                if (m_bitmap != null)
                {
                    m_bitmap.Dispose();
                }
                m_bitmap = new Bitmap(picture);
            }
        }

        public void DrawGraphics(PXCMFaceData moduleOutput, PXCMFaceData lastmoduleOutput)
        {
            Debug.Assert(moduleOutput != null);

            //zz


            for (var i = 0; i < moduleOutput.QueryNumberOfDetectedFaces(); i++)
            {
                PXCMFaceData.Face face = moduleOutput.QueryFaceByIndex(i);
                PXCMFaceData.Face lastface = moduleOutput.QueryFaceByIndex(i);


                if (face == null)
                {
                    throw new Exception("DrawGraphics::PXCMFaceData.Face null");
                }
                //
                //Savedata(moduleOutput);


                lock (m_bitmapLock)
                {
                    m_faceTextOrganizer.ChangeFace(i, face, m_bitmap.Height, m_bitmap.Width);
                }

                //DrawLocation(face);
                //DrawLandmark(face);
                //DrawPose(face);
                //绘制分辨率为1280*720的图像
                //DrawPulse(face);
                //DrawExpressions(face);
            }
        }
        //zz

        #region  Record

        private void Live_Click(object sender, EventArgs e)
        {
            Record.Checked = false;
            Live.Checked = true;
        }
        private void Record_Click(object sender, EventArgs e)
        {
            Live.Checked = false;
            Record.Checked = true;
            try
            {   
                string time = DateTime.Now.ToString("yyyy-MM-dd");
                m_filename = Environment.CurrentDirectory + "\\data\\" +sessionID+ "\\record.rssdk";
            }
            catch (Exception)
            {
                throw;
            }
        }

        public bool GetRecordState()
        {
            return Record.Checked;
        }

        private delegate string GetFileDelegate();

        #endregion

        #region Modules Drawing

        private static readonly Assembly m_assembly = Assembly.GetExecutingAssembly();

        private readonly ResourceSet m_resources =
            new ResourceSet(m_assembly.GetManifestResourceStream(@"DF_FaceTracking.cs.Properties.Resources.resources"));

        private readonly Dictionary<PXCMFaceData.ExpressionsData.FaceExpression, Bitmap> m_cachedExpressions =
            new Dictionary<PXCMFaceData.ExpressionsData.FaceExpression, Bitmap>();

        private readonly Dictionary<PXCMFaceData.ExpressionsData.FaceExpression, string> m_expressionDictionary =
            new Dictionary<PXCMFaceData.ExpressionsData.FaceExpression, string>
            {
                {PXCMFaceData.ExpressionsData.FaceExpression.EXPRESSION_MOUTH_OPEN, @"MouthOpen"},
                {PXCMFaceData.ExpressionsData.FaceExpression.EXPRESSION_SMILE, @"Smile"},
                {PXCMFaceData.ExpressionsData.FaceExpression.EXPRESSION_KISS, @"Kiss"},
                {PXCMFaceData.ExpressionsData.FaceExpression.EXPRESSION_EYES_UP, @"Eyes_Turn_Up"},
                {PXCMFaceData.ExpressionsData.FaceExpression.EXPRESSION_EYES_DOWN, @"Eyes_Turn_Down"},
                {PXCMFaceData.ExpressionsData.FaceExpression.EXPRESSION_EYES_TURN_LEFT, @"Eyes_Turn_Left"},
                {PXCMFaceData.ExpressionsData.FaceExpression.EXPRESSION_EYES_TURN_RIGHT, @"Eyes_Turn_Right"},
                {PXCMFaceData.ExpressionsData.FaceExpression.EXPRESSION_EYES_CLOSED_LEFT, @"Eyes_Closed_Left"},
                {PXCMFaceData.ExpressionsData.FaceExpression.EXPRESSION_EYES_CLOSED_RIGHT, @"Eyes_Closed_Right"},
                {PXCMFaceData.ExpressionsData.FaceExpression.EXPRESSION_BROW_LOWERER_RIGHT, @"Brow_Lowerer_Right"},
                {PXCMFaceData.ExpressionsData.FaceExpression.EXPRESSION_BROW_LOWERER_LEFT, @"Brow_Lowerer_Left"},
                {PXCMFaceData.ExpressionsData.FaceExpression.EXPRESSION_BROW_RAISER_RIGHT, @"Brow_Raiser_Right"},
                {PXCMFaceData.ExpressionsData.FaceExpression.EXPRESSION_BROW_RAISER_LEFT, @"Brow_Raiser_Left"}
            };

        public void DrawLocation(PXCMFaceData.Face face)
        {
            Debug.Assert(face != null);
            if (m_bitmap == null ) return;

            PXCMFaceData.DetectionData detection = face.QueryDetection();
            if (detection == null)
                return;

            lock (m_bitmapLock)
            {
                using (Graphics graphics = Graphics.FromImage(m_bitmap))
                using (var pen = new Pen(m_faceTextOrganizer.Colour, 3.0f))
                using (var brush = new SolidBrush(m_faceTextOrganizer.Colour))
                using (var font = new Font(FontFamily.GenericMonospace, m_faceTextOrganizer.FontSize, FontStyle.Bold))
                {
                    graphics.DrawRectangle(pen, m_faceTextOrganizer.RectangleLocation);
                    String faceId = String.Format("Face ID: {0}",
                        face.QueryUserID().ToString(CultureInfo.InvariantCulture));
                    graphics.DrawString(faceId, font, brush, m_faceTextOrganizer.FaceIdLocation);
                }
            }
        }

        PXCMFaceData.LandmarkPoint[] _lastpoints;
        public void DrawLandmark(PXCMFaceData.Face face)
        {
            Debug.Assert(face != null);
            PXCMFaceData.LandmarksData landmarks = face.QueryLandmarks();

            ///zz
            if (isStart)
            {
                SaveFeature(face,FaceTracking.m_frame);
            }
            bool tag = true;  //是否小于阈值的tag
            
            if (m_bitmap == null || landmarks == null) return;

            lock (m_bitmapLock)
            {
                using (Graphics graphics = Graphics.FromImage(m_bitmap))
                using (var brush = new SolidBrush(Color.White))
                using (var midConfidenceBrush = new SolidBrush(Color.Blue))
                using (var lowConfidenceBrush = new SolidBrush(Color.Red))
                using (var font = new Font(FontFamily.GenericMonospace, m_faceTextOrganizer.FontSize, FontStyle.Bold))
                {
                    PXCMFaceData.LandmarkPoint[] points;
                    
                    bool res = landmarks.QueryPoints(out points);

                    if (_lastpoints == null)
                        _lastpoints = points;

                    Debug.Assert(res);

                    var point = new PointF();

                    int[] a = new int[32] { 76, 77, 12, 16, 14, 10, 20, 24, 18, 22, 71, 0, 4, 74, 5, 9, 29, 26, 31, 30, 32, 39, 33, 47, 46, 48, 51, 52, 50, 56, 66, 61 };

                    for (int i = 0; i < 78; i++)
                    {
                        int k = 31;
                        point.X = points[i].image.x + LandmarkAlignment;
                        point.Y = points[i].image.y + LandmarkAlignment;

                        if (points[i].confidenceImage == 0)
                            graphics.DrawString("x", font, lowConfidenceBrush, point);

                        graphics.DrawString(i.ToString(), font, brush, point);

                        while (k > -1)
                        {
                            if (a[k] == i)
                                graphics.DrawString(i.ToString(), font, lowConfidenceBrush, point);

                            k--;

                        }
                        if (PositionChange(points[i].image, _lastpoints[i].image) > 1)
                        {
                            //graphics.DrawString("o", font, brush, point);
                            if (tag == true)
                                tag = false;
                        }
                    }

                    if(RegisterThisID == true)
                    {
                        Savedata_less(face, FaceTracking.m_frame);
                        RegisterThisID = false;
                    }

                    if (!tag && isStart)
                    {
                        Savedata_less(face, FaceTracking.m_frame);
                        
                    }
                    _lastpoints = points;
                   
                    //显示z的距离，鼻尖到摄像头的距离
                    string status="提示";
                    string tip = "";
                    if (points[29].world.z < 0.26)
                    {
                        tip = "√ ";
                        Savedata_txt.Invoke(new UpdateStatusDelegate(delegate (string s) { Savedata_txt.Enabled = true; }), new object[] { status });
                    }
                        
                    else
                    {
                        tip = "X ";
                        Savedata_txt.Invoke(new UpdateStatusDelegate(delegate (string s) { Savedata_txt.Enabled = false; }), new object[] { status });
                    }
                }
                
            }
        }

        //zz
        private void Savedata_txt_KeyDown(object sender, KeyEventArgs e)
        {

            if (e.KeyCode == Keys.F1)
            {
                Stopped = true;
                isStart = false;
            }
        }

        public float PositionChange(PXCMPoint3DF32 world, PXCMPoint3DF32 lastworld)
        {
            float ans=0.0F;
            ans = (float)Math.Sqrt((world.x - lastworld.x) * (world.x - lastworld.x) + (world.y - lastworld.y) * (world.y - lastworld.y) + (world.z - lastworld.z) * (world.z - lastworld.z));
            return ans;
        }

        public float PositionChange(PXCMPointF32 image, PXCMPointF32 lastimage)
        {
            float ans = 0.0F;
            ans = (float)Math.Sqrt((image.x - lastimage.x) * (image.x - lastimage.x) + (image.y - lastimage.y) * (image.y - lastimage.y));
            return ans;
        }

        public void DrawPose(PXCMFaceData.Face face)
        {
            Debug.Assert(face != null);
            PXCMFaceData.PoseEulerAngles poseAngles;
            PXCMFaceData.PoseData pdata = face.QueryPose();
            if (pdata == null)
            {
                return;
            }
            if (!pdata.QueryPoseAngles(out poseAngles)) return;

            lock (m_bitmapLock)
            {
                using (Graphics graphics = Graphics.FromImage(m_bitmap))
                using (var brush = new SolidBrush(m_faceTextOrganizer.Colour))
                using (var font = new Font(FontFamily.GenericMonospace, m_faceTextOrganizer.FontSize, FontStyle.Bold))
                {
                    string yawText = String.Format("Yaw = {0}",
                        Convert.ToInt32(poseAngles.yaw).ToString(CultureInfo.InvariantCulture));
                    graphics.DrawString(yawText, font, brush, m_faceTextOrganizer.PoseLocation.X,
                        m_faceTextOrganizer.PoseLocation.Y);

                    string pitchText = String.Format("Pitch = {0}",
                        Convert.ToInt32(poseAngles.pitch).ToString(CultureInfo.InvariantCulture));
                    graphics.DrawString(pitchText, font, brush, m_faceTextOrganizer.PoseLocation.X,
                        m_faceTextOrganizer.PoseLocation.Y + m_faceTextOrganizer.FontSize);

                    string rollText = String.Format("Roll = {0}",
                        Convert.ToInt32(poseAngles.roll).ToString(CultureInfo.InvariantCulture));
                    graphics.DrawString(rollText, font, brush, m_faceTextOrganizer.PoseLocation.X,
                        m_faceTextOrganizer.PoseLocation.Y + 2 * m_faceTextOrganizer.FontSize);
                }
            }
        }

        public void DrawExpressions(PXCMFaceData.Face face)
        {
            Debug.Assert(face != null);
            if (m_bitmap == null ) return;

            PXCMFaceData.ExpressionsData expressionsOutput = face.QueryExpressions();

            if (expressionsOutput == null) return;

            lock (m_bitmapLock)
            {
                using (Graphics graphics = Graphics.FromImage(m_bitmap))
                using (var brush = new SolidBrush(m_faceTextOrganizer.Colour))
                {
                    const int imageSizeWidth = 18;
                    const int imageSizeHeight = 18;

                    int positionX = m_faceTextOrganizer.ExpressionsLocation.X;
                    int positionXText = positionX + imageSizeWidth;
                    int positionY = m_faceTextOrganizer.ExpressionsLocation.Y;
                    int positionYText = positionY + imageSizeHeight / 4;

                    foreach (var expressionEntry in m_expressionDictionary)
                    {
                        PXCMFaceData.ExpressionsData.FaceExpression expression = expressionEntry.Key;
                        PXCMFaceData.ExpressionsData.FaceExpressionResult result;
                        bool status = expressionsOutput.QueryExpression(expression, out result);
                        if (!status) continue;

                        Bitmap cachedExpressionBitmap;
                        bool hasCachedExpressionBitmap = m_cachedExpressions.TryGetValue(expression, out cachedExpressionBitmap);
                        if (!hasCachedExpressionBitmap)
                        {
                            cachedExpressionBitmap = (Bitmap)m_resources.GetObject(expressionEntry.Value);
                            m_cachedExpressions.Add(expression, cachedExpressionBitmap);
                        }

                        using (var font = new Font(FontFamily.GenericMonospace, m_faceTextOrganizer.FontSize, FontStyle.Bold))
                        {
                            Debug.Assert(cachedExpressionBitmap != null, "cachedExpressionBitmap != null");
                            graphics.DrawImage(cachedExpressionBitmap, new Rectangle(positionX, positionY, imageSizeWidth, imageSizeHeight));
                            var expressionText = String.Format("= {0}", result.intensity);
                            graphics.DrawString(expressionText, font, brush, positionXText, positionYText);

                            positionY += imageSizeHeight;
                            positionYText += imageSizeHeight;
                        }
                    }
                }
            }
        }

        private void DrawPulse(PXCMFaceData.Face face)
        {
            Debug.Assert(face != null);
            if (m_bitmap == null || true) return;

            var pulseData = face.QueryPulse();
            if (pulseData == null)
                return;

            lock (m_bitmapLock)
            {
                var pulseString = "Pulse: " + pulseData.QueryHeartRate();

                using (var graphics = Graphics.FromImage(m_bitmap))
                using (var brush = new SolidBrush(m_faceTextOrganizer.Colour))
                using (var font = new Font(FontFamily.GenericMonospace, m_faceTextOrganizer.FontSize, FontStyle.Bold))
                {
                    graphics.DrawString(pulseString, font, brush, m_faceTextOrganizer.PulseLocation);
                }
            }

        }

        #endregion

        private delegate void DoTrackingCompleted();
        //更新图像的委托
        private delegate void UpdatePanelDelegate();

        private delegate void UpdateStatusDelegate(string status);

        public bool IsPulseEnabled()
        {
            return false;
        }
        private void Savedata_txt_Click(object sender, EventArgs e)
        {
            expressionNumber++;
            isStart = true;

        }

        private void SaveFeature(PXCMFaceData.Face qface, int frameCount) {
            PXCMFaceData.LandmarksData ldata = qface.QueryLandmarks();
            PXCMFaceData.ExpressionsData edata = qface.QueryExpressions();
            PXCMRectI32 rect;
            // get the landmark data
            var landmarkGroupTypes = Enum.GetValues(typeof(PXCMFaceData.LandmarksGroupType)).Cast<PXCMFaceData.LandmarksGroupType>();
            qface.QueryDetection().QueryBoundingRect(out rect);
            //保存时间戳
            string time = DateTime.Now.ToString("yyyy-MM-dd") + " " + DateTime.Now.ToString("HH:mm:ss:fff");
            sw_feature.Write(frameCount.ToString() + ' ' + time + ' ');
            // 对于每个LandmarkPoint转换成成员变量中的world属性
          
                PXCMFaceData.LandmarkPoint[] points;
                ldata.QueryPoints(out points);
                for (int i = 0; i < 78; i++)
                {
                    //把脸部特征放入数组中
                    double position_x = Math.Round(points[i].image.x - rect.x, 4);
                    double position_y = Math.Round(points[i].image.y - rect.y, 4);
                    double position_z = Math.Round(points[i].world.z, 5);

                    sw_feature.Write(position_x.ToString()+ ' '+ position_y.ToString() + ' '+ position_z.ToString() + ' ');
                }
            for (int i = 0; i < 22; i++)
            {
                double emotion=0;
                if (edata != null) {
                    PXCMFaceData.ExpressionsData.FaceExpressionResult score;
                    edata.QueryExpression((PXCMFaceData.ExpressionsData.FaceExpression)i, out score);
                    Expression.facialExpressionIndensity[i] = score.intensity;
                    emotion = score.intensity;
                }
                sw_feature.Write(emotion.ToString()+ ' ');
            }
            sw_feature.WriteLine();
        }
        /// <summary>
        /// 保存筛选32点的数据
        /// </summary>
        /// <param name="qface"></param>
        /// <param name="frameCount"></param>
        private void Savedata_less(PXCMFaceData.Face qface, int frameCount)
        {
            ////zz

            PXCMFaceData.PoseData posedata = qface.QueryPose();
            PXCMFaceData.LandmarksData Idata = qface.QueryLandmarks();
            PXCMFaceData.LandmarkPoint[] points;
            PXCMFaceData.PoseEulerAngles angles;
            PXCMFaceData.HeadPosition headpostion;

            posedata.QueryPoseAngles(out angles);
            posedata.QueryHeadPosition(out headpostion);
            Idata.QueryPoints(out points);

            string time = DateTime.Now.ToString("yyyy-MM-dd") + " " + DateTime.Now.ToString("hh:mm:ss:fff");
            sw_less.Write(frameCount.ToString().PadRight(5) + '\t' + time + '\t' + expressionNumber.ToString() + '\t');

            int[] a = new int[32] { 76, 77, 12, 16, 14, 10, 20, 24, 18, 22, 71, 0, 4, 74, 5, 9, 29, 26, 31, 30, 32, 39, 33, 47, 46, 48, 51, 52, 50, 56, 66, 61 }; 
            int t = 0;

            for (int i = 0; i < 32; i++)
            {

                //string LandmarkPointName = "points[a[t]].source.index";
                string LandmarkPointName = MarkPointName(points[a[t]].source.index);
                float Positionworld_x = points[a[t]].world.x;
                float Positionworld_y = points[a[t]].world.y;
                float Positionworld_z = points[a[t]].world.z;

                float PositionImage_x = points[a[t]].image.x;
                float PositionImage_y = points[a[t++]].image.y;

                sw_less.Write((a[i]).ToString() + '\t'
                    + LandmarkPointName.ToString() + '\t'
                    + Positionworld_x.ToString().PadRight(25) + '\t'
                    + Positionworld_y.ToString().PadRight(25) + '\t'
                    + Positionworld_z.ToString().PadRight(25) + '\t'
                    + PositionImage_x.ToString().PadRight(25) + '\t'
                    + PositionImage_y.ToString().PadRight(25) + '\t');
            }

            float HeadCenter_x = headpostion.headCenter.x;
            float HeadCenter_y = headpostion.headCenter.y;
            float HeadCenter_z = headpostion.headCenter.z;

            float PoseEulerAngles_pitch = angles.pitch;
            float PoseEulerAngles_roll = angles.roll;
            float PoseEulerAngles_yaw = angles.yaw;

            sw_less.Write(HeadCenter_x.ToString().PadRight(25) + '\t'
                + HeadCenter_y.ToString().PadRight(25) + '\t'
                + HeadCenter_z.ToString().PadRight(25) + '\t'
                + PoseEulerAngles_pitch.ToString().PadRight(25) + '\t'
                + PoseEulerAngles_roll.ToString().PadRight(25) + '\t'
                + PoseEulerAngles_yaw.ToString().PadRight(25) + '\t');

            sw_less.WriteLine();

        }

        private string MarkPointName(int index)
        {
            string PName = null;
            switch (index)
            {
                case 71:
                    PName = "EYEBROW_RIGHT_CENTER"; break;
                case 0:
                    PName = "EYEBROW_RIGHT_RIGHT"; break;
                case 4:
                    PName = "EYEBROW_RIGHT_LEFT"; break;
                case 5:
                    PName = "EYEBROW_LEFT_RIGHT"; break;
                case 9:
                    PName = "EYEBROW_LEFT_LEFT"; break;

                case 76:
                    PName = "EYE_RIGHT_CENTER"; break;
                case 77:
                    PName = "EYE_LEFT_CENTER"; break;

                case 12:
                    PName = "EYELID_RIGHT_TOP"; break;
                case 16:
                    PName = "EYELID_RIGHT_BOTTOM"; break;
                case 14:
                    PName = "EYELID_RIGHT_RIGHT"; break;
                case 10:
                    PName = "EYELID_RIGHT_LEFT"; break;

                case 20:
                    PName = "EYELID_LEFT_TOP"; break;
                case 24:
                    PName = "EYELID_LEFT_BOTTOM"; break;
                case 18:
                    PName = "EYELID_LEFT_RIGHT"; break;
                case 22:
                    PName = "EYELID_LEFT_LEFT"; break;

                case 29:
                    PName = "NOSE_TIP"; break;
                case 26:
                    PName = "NOSE_TOP"; break;
                case 31:
                    PName = "NOSE_BOTTOM"; break;
                case 30:
                    PName = "NOSE_RIGHT"; break;
                case 32:
                    PName = "NOSE_LEFT"; break;

                case 39:
                    PName = "LIP_RIGHT"; break;
                case 33:
                    PName = "LIP_LEFT"; break;

                case 47:
                    PName = "UPPER_LIP_CENTER"; break;
                case 46:
                    PName = "UPPER_LIP_RIGHT"; break;
                case 48:
                    PName = "UPPER_LIP_LEFT"; break;

                case 51:
                    PName = "LOWER_LIP_CENTER"; break;
                case 52:
                    PName = "LOWER_LIP_RIGHT"; break;
                case 50:
                    PName = "LOWER_LIP_LEFT"; break;

                case 56:
                    PName = "FACE_BORDER_TOP_RIGHT"; break;
                case 66:
                    PName = "FACE_BORDER_TOP_LEFT"; break;
                case 61:
                    PName = "CHIN"; break;
                case 74:
                    PName = "EYEBROW_LEFT_CENTER"; break;

                default:
                    PName = "NOT_USE";
                    break;
            }

            return PName;
        }
        #region 字段
        int expressionNumber = 0;
        bool isStart = false;
        StreamWriter sw_less;
        StreamWriter sw_feature;
        #endregion
    }
}
