using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Windows.Forms;
using System.Data;
using System.Data.SqlClient;
using System.Data.OleDb;
using System.Reflection;

namespace DF_FaceTracking.cs
{
    internal class FaceTracking
    {
        private readonly MainForm m_form;
        private FPSTimer m_timer;
        private bool m_wasConnected;
        public static int m_frame = 0;



        public FaceTracking(MainForm form)
        {
            m_form = form;
            //zz
            //Savedata();
        }

        private void DisplayDeviceConnection(bool isConnected)
        {
            if (isConnected && !m_wasConnected) m_form.UpdateStatus("Device Reconnected", MainForm.Label.StatusLabel);
            else if (!isConnected && m_wasConnected)
                m_form.UpdateStatus("Device Disconnected", MainForm.Label.StatusLabel);
            m_wasConnected = isConnected;
        }

        private void DisplayPicture(PXCMImage image)
        {
            PXCMImage.ImageData data;
            if (image.AcquireAccess(PXCMImage.Access.ACCESS_READ, PXCMImage.PixelFormat.PIXEL_FORMAT_RGB32, out data) <
                pxcmStatus.PXCM_STATUS_NO_ERROR) return;
            m_form.DrawBitmap(data.ToBitmap(0, image.info.width, image.info.height));
            m_timer.Tick("");
            image.ReleaseAccess(data);
        }

        private void CheckForDepthStream(PXCMCapture.Device.StreamProfileSet profiles, PXCMFaceModule faceModule)
        {
            PXCMFaceConfiguration faceConfiguration = faceModule.CreateActiveConfiguration();
            if (faceConfiguration == null)
            {
                Debug.Assert(faceConfiguration != null);
                return;
            }

            PXCMFaceConfiguration.TrackingModeType trackingMode = faceConfiguration.GetTrackingMode();
            faceConfiguration.Dispose();

            if (trackingMode != PXCMFaceConfiguration.TrackingModeType.FACE_MODE_COLOR_PLUS_DEPTH
                || trackingMode != PXCMFaceConfiguration.TrackingModeType.FACE_MODE_IR)
                return;
            if (profiles.depth.imageInfo.format == PXCMImage.PixelFormat.PIXEL_FORMAT_DEPTH) return;
            PXCMCapture.DeviceInfo dinfo;
            m_form.Devices.TryGetValue(m_form.GetCheckedDevice(), out dinfo);

            if (dinfo != null)
                MessageBox.Show(
                    String.Format("Depth stream is not supported for device: {0}. \nUsing 2D tracking", dinfo.name),
                    @"Face Tracking",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void FaceAlertHandler(PXCMFaceData.AlertData alert)
        {
            m_form.UpdateStatus(alert.label.ToString(), MainForm.Label.StatusLabel);
        }

        public void SimplePipeline()
        {
            PXCMSenseManager pp = m_form.Session.CreateSenseManager();

            if (pp == null)
            {
                throw new Exception("PXCMSenseManager null");
            }

            PXCMCaptureManager captureMgr = pp.captureManager;
            if (captureMgr == null)
            {
                throw new Exception("PXCMCaptureManager null");
            }

            var selectedRes = m_form.GetCheckedColorResolution();
            if (selectedRes != null )  
            {
                // Set active camera
                PXCMCapture.DeviceInfo deviceInfo;
                m_form.Devices.TryGetValue(m_form.GetCheckedDevice(), out deviceInfo);
                captureMgr.FilterByDeviceInfo(m_form.GetCheckedDeviceInfo());

                // activate filter only live/record mode , no need in playback mode
                var set = new PXCMCapture.Device.StreamProfileSet
                {
                    color =
                    {
                        frameRate = selectedRes.Item2,
                        imageInfo =
                        {
                            format = selectedRes.Item1.format,
                            height = selectedRes.Item1.height,
                            width = selectedRes.Item1.width
                        }
                    }
                };

                if (m_form.IsPulseEnabled() && (set.color.imageInfo.width < 1280 || set.color.imageInfo.height < 720))
                {
                    captureMgr.FilterByStreamProfiles(PXCMCapture.StreamType.STREAM_TYPE_COLOR, 1280, 720, 0);
                }
                else
                {
                    captureMgr.FilterByStreamProfiles(set);
                }
            }

            // Set Source & Landmark Profile Index 
            if (m_form.GetRecordState())
            {
                captureMgr.SetFileName(m_form.GetFileName(), true);
            }
            
            // Set Module            
            pp.EnableFace();
            PXCMFaceModule faceModule = pp.QueryFace();
            if (faceModule == null)
            {
                Debug.Assert(faceModule != null);
                return;
            }
            
            PXCMFaceConfiguration moduleConfiguration = faceModule.CreateActiveConfiguration();

            if (moduleConfiguration == null)
            {
                Debug.Assert(moduleConfiguration != null);
                return;
            }

            var checkedProfile = m_form.GetCheckedProfile();
            var mode = m_form.FaceModesMap.First(x => x.Value == checkedProfile).Key;
            
            moduleConfiguration.SetTrackingMode(mode);

            moduleConfiguration.strategy = PXCMFaceConfiguration.TrackingStrategyType.STRATEGY_RIGHT_TO_LEFT;

            moduleConfiguration.detection.maxTrackedFaces = 4;
            moduleConfiguration.landmarks.maxTrackedFaces = 4;
            moduleConfiguration.pose.maxTrackedFaces = 4;
            
            PXCMFaceConfiguration.ExpressionsConfiguration econfiguration = moduleConfiguration.QueryExpressions();
            if (econfiguration == null)
            {
                throw new Exception("ExpressionsConfiguration null");
            }
            econfiguration.properties.maxTrackedFaces = 4;

            econfiguration.EnableAllExpressions();
            moduleConfiguration.detection.isEnabled = true;
            moduleConfiguration.landmarks.isEnabled = true;
            moduleConfiguration.pose.isEnabled = true;
            
                econfiguration.Enable();
            

            PXCMFaceConfiguration.PulseConfiguration pulseConfiguration = moduleConfiguration.QueryPulse();
            if (pulseConfiguration == null)
            {
                throw new Exception("pulseConfiguration null");
            }
			
            pulseConfiguration.properties.maxTrackedFaces = 4;
            if (m_form.IsPulseEnabled())
            {
                pulseConfiguration.Enable();
            }

            PXCMFaceConfiguration.RecognitionConfiguration qrecognition = moduleConfiguration.QueryRecognition();
            if (qrecognition == null)
            {
                throw new Exception("PXCMFaceConfiguration.RecognitionConfiguration null");
            }
            if (m_form.IsRecognitionChecked())
            {
                qrecognition.Enable();
            }

            moduleConfiguration.EnableAllAlerts();
            moduleConfiguration.SubscribeAlert(FaceAlertHandler);

            pxcmStatus applyChangesStatus = moduleConfiguration.ApplyChanges();

            m_form.UpdateStatus("Init Started", MainForm.Label.StatusLabel);

            if (applyChangesStatus < pxcmStatus.PXCM_STATUS_NO_ERROR || pp.Init() < pxcmStatus.PXCM_STATUS_NO_ERROR)
            {
                m_form.UpdateStatus("Init Failed", MainForm.Label.StatusLabel);
            }
            else
            {
                using (PXCMFaceData moduleOutput = faceModule.CreateOutput())
                {
                    PXCMFaceData  lastmoduleOutput = moduleOutput; ;
                    Debug.Assert(moduleOutput != null);
                    PXCMCapture.Device.StreamProfileSet profiles;
                    PXCMCapture.Device device  =  captureMgr.QueryDevice();

                    if (device == null)
                    {
                        throw new Exception("device null");
                    }
                    
                    device.QueryStreamProfileSet(PXCMCapture.StreamType.STREAM_TYPE_DEPTH, 0, out profiles);
                    CheckForDepthStream(profiles, faceModule);
                    
                    m_form.UpdateStatus("Streaming", MainForm.Label.StatusLabel);
                    m_timer = new FPSTimer(m_form);

                    while (!m_form.Stopped)
                    {
                        m_frame++;

                        if (pp.AcquireFrame(true) < pxcmStatus.PXCM_STATUS_NO_ERROR) break;
                        var isConnected = pp.IsConnected();
                        DisplayDeviceConnection(isConnected);
                        if (isConnected)
                        {
                            var sample = pp.QueryFaceSample();
                            if (sample == null)
                            {
                                pp.ReleaseFrame();
                                continue;
                            }
                            switch (mode)
                            {
                                case PXCMFaceConfiguration.TrackingModeType.FACE_MODE_IR:
                                    if (sample.ir != null)
                                    DisplayPicture(sample.ir);
                                    break;
                                default:
                                    DisplayPicture(sample.color);
                                    break;
                            }


                            moduleOutput.Update();
                            PXCMFaceConfiguration.RecognitionConfiguration recognition = moduleConfiguration.QueryRecognition();
                            if (recognition == null)
                            {
                                pp.ReleaseFrame();
                                continue;
                            }


                            m_form.DrawGraphics(moduleOutput,lastmoduleOutput);
                            lastmoduleOutput = moduleOutput;
                            m_form.UpdatePanel();
                        }
                        pp.ReleaseFrame();
                    }

                }

                m_form.UpdateStatus("Stopped", MainForm.Label.StatusLabel);
            }
            moduleConfiguration.Dispose();
            pp.Close();
            pp.Dispose();
        }

        //zz
        private void Savedata(PXCMFaceData faceOutput)
        {
            ////zz
            PXCMFaceData.Face qface = faceOutput.QueryFaceByIndex(0);
            PXCMFaceData.PoseData posedata = qface.QueryPose();
            PXCMFaceData.LandmarksData Idata = qface.QueryLandmarks();
            PXCMFaceData.LandmarkPoint[] points;
            PXCMFaceData.PoseEulerAngles angles;
            PXCMFaceData.HeadPosition headpostion;

            posedata.QueryPoseAngles(out angles);
            posedata.QueryHeadPosition(out headpostion);
            Idata.QueryPoints(out points);
            

            string connSting;
            connSting = "server=localhost;database=RealSense;Integrated Security=True ";
            SqlConnection sConn = new SqlConnection(connSting);
            try
            {
                sConn.Open();
            }
            catch (Exception ex)
            {
                Console.WriteLine("链接错误:" + ex.Message);
            }

            int[] a = new int[32] { 76, 77, 12, 16, 14, 10, 20, 24, 18, 22, 70, 0, 4, 7, 5, 9, 29, 26, 31, 30, 32, 39, 33, 47, 46, 48, 51, 52, 50, 56, 65, 61 };
            int t = 0;

            for (int i = 0; i < 32; i++)
            {
                string sql_Insert;

                string times = DateTime.Now.ToString("yyyy-MM-dd") + " " + DateTime.Now.ToString("hh:mm:ss");

                string LandmarkPointName = MarkPointName(points[a[t]].source.index);
                
                float Positionworld_x = points[a[t]].world.x;
                float Positionworld_y = points[a[t]].world.y;
                float Positionworld_z = points[a[t]].world.z;

                float PositionImage_x = points[a[t]].image.x;
                float PositionImage_y = points[a[t++]].image.y;

                float HeadCenter_x = headpostion.headCenter.x;
                float HeadCenter_y = headpostion.headCenter.y;
                float HeadCenter_z = headpostion.headCenter.z;

                float PoseEulerAngles_pitch = angles.pitch;
                float PoseEulerAngles_roll = angles.roll;
                float PoseEulerAngles_yaw = angles.yaw;

                sql_Insert = "insert into FaceData(time,LandmarkPointName,[Positionworld.x],[Positionworld.y],[Positionworld.z],[PositionImage.x],[PositionImage.y],[HeadCenter.x],[HeadCenter.y],[HeadCenter.z],[PoseEulerAngles.pitch],[PoseEulerAngles.roll],[PoseEulerAngles.yaw])values('"
                    + times + "','"
                    + LandmarkPointName + "','"
                    + Positionworld_x + "','"
                    + Positionworld_y + "','"
                    + Positionworld_z + "','"
                    + PositionImage_x + "','"
                    + PositionImage_y + "','"
                    + HeadCenter_x + "','"
                    + HeadCenter_y + "','"
                    + HeadCenter_z + "','"
                    + PoseEulerAngles_pitch + "','"
                    + PoseEulerAngles_roll + "','"
                    + PoseEulerAngles_yaw + "')";             

                SqlCommand sCmd = new SqlCommand(sql_Insert, sConn);
                sCmd.ExecuteNonQuery();
               
            }
            sConn.Close();
        }

        private string MarkPointName(int index)
        {
            string PName = null;
            switch (index)
            {
                case 70:
                    PName = "EYEBROW_RIGHT_CENTER";break;
                case 0:
                    PName= "EYEBROW_RIGHT_RIGHT"; break;
                case 4:
                    PName = "EYEBROW_RIGHT_LEFT";break;
                case 5:
                    PName = "EYEBROW_LEFT_RIGHT";break;
                case 7:
                    PName = "EYEBROW_LEFT_CENTER";break;
                case 9:
                    PName = "EYEBROW_LEFT_LEFT";break;

                case 76:
                    PName = "EYE_RIGHT_CENTER";break;
                case 77:
                    PName = "EYE_LEFT_CENTER";break;

                case 12:
                    PName = "EYELID_RIGHT_TOP";break;
                case 16:
                    PName = "EYELID_RIGHT_BOTTOM";break;
                case 14:
                    PName = "EYELID_RIGHT_RIGHT";break;
                case 10:
                    PName = "EYELID_RIGHT_LEFT";break;

                case 20:
                    PName = "EYELID_LEFT_TOP";break;
                case 24:
                    PName = "EYELID_LEFT_BOTTOM";break;
                case 18:
                    PName = "EYELID_LEFT_RIGHT";break;
                case 22:
                    PName = "EYELID_LEFT_LEFT";break;

                case 29:
                    PName = "NOSE_TIP";break;
                case 26:
                    PName = "NOSE_TOP";break;
                case 31:
                    PName = "NOSE_BOTTOM";break;
                case 30:
                    PName = "NOSE_RIGHT";break;
                case 32:
                    PName = "NOSE_LEFT";break;

                case 39:
                    PName = "LIP_RIGHT";break;
                case 33:
                    PName = "LIP_LEFT";break;

                case 47:
                    PName = "UPPER_LIP_CENTER";break;
                case 46:
                    PName = "UPPER_LIP_RIGHT";break;
                case 48:
                    PName = "UPPER_LIP_LEFT";break;

                case 51:
                    PName = "LOWER_LIP_CENTER";break;
                case 52:
                    PName = "LOWER_LIP_RIGHT";break;
                case 50:
                    PName = "LOWER_LIP_LEFT";break;

                case 56:
                    PName = "FACE_BORDER_TOP_RIGHT";break;
                case 65:
                    PName = "FACE_BORDER_TOP_LEFT";break;
                case 61:
                    PName = "CHIN";break;

                default:
                    PName = "NOT_USE";
                    break;
            }

            return PName;
        }
    }
}