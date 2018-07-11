using System.Drawing;

namespace DF_FaceTracking.cs
{
    public class FaceTextOrganizer
    {
        private readonly Color[] m_colorList =
        {
            Color.Yellow, Color.Orange, Color.Lime, Color.Magenta, Color.Brown,
            Color.Turquoise, Color.DeepSkyBlue
        };

        private Color m_color;
        private Point m_expression;
        private Point m_faceId;
        private int m_imageWidth;
        private Point m_pose;
        private Point m_recognitionId;
        private Point m_pulse;
        private PXCMRectI32 m_rectangle;

        public PointF RecognitionLocation
        {
            get { return m_recognitionId; }
        }

        public PointF FaceIdLocation
        {
            get { return m_faceId; }
        }

        public PointF PoseLocation
        {
            get { return m_pose; }
        }

        public PointF PulseLocation
        {
            get { return m_pulse; }
        }

        public Point ExpressionsLocation
        {
            get { return m_expression; }
        }

        public Rectangle RectangleLocation
        {
            get { return new Rectangle(m_rectangle.x, m_rectangle.y, m_rectangle.w, m_rectangle.h); }
        }

        public Color Colour
        {
            get { return m_color; }
        }

        public int FontSize
        {
            get { return CalculateDefiniteFontSize(); }
        }

        private int CalculateDefiniteFontSize()
        {
            switch (m_imageWidth)
            {
                case 640:
                case 848:
                case 960:
                    return 6;
                case 1280:
                    return 8;
                case 1920:
                    return 12;
                default:
                    return 12;
            }
        }

        public void ChangeFace(int faceIndex, PXCMFaceData.Face face, int imageHeight, int imageWidth)
        {
            const int threshold = 5;
            const int expressionThreshold = 55;
            const int faceTextWidth = 100;
            const int textHeightThreshold = 30;
            
            m_imageWidth = imageWidth;

            PXCMFaceData.DetectionData  fdetectionData = face.QueryDetection();
            m_color = m_colorList[faceIndex % m_colorList.Length];

            if (fdetectionData == null)
            {
                int currentWidth = faceIndex * faceTextWidth;  

                m_recognitionId.X = threshold + currentWidth;
                m_recognitionId.Y = threshold;

                m_pose.X = threshold + currentWidth;
                m_pose.Y = m_recognitionId.Y + 3 * threshold;

                m_pulse.X = threshold + currentWidth;
                m_pulse.Y = m_pose.Y + 6 * threshold;

                m_expression.X = threshold + currentWidth;
                m_expression.Y = m_pulse.Y + threshold + textHeightThreshold;
            }
            else
            {
                fdetectionData.QueryBoundingRect(out m_rectangle);                

                m_recognitionId.X = m_rectangle.x + threshold;
                m_recognitionId.Y = m_rectangle.y + CalculateDefiniteFontSize() + threshold;

                m_faceId.X = m_rectangle.x + threshold;
                m_faceId.Y = m_rectangle.y + threshold;

                m_pose.X = m_rectangle.x + threshold;
                m_pose.Y = m_rectangle.y + m_rectangle.h - 3 * CalculateDefiniteFontSize() - 2 * threshold;

                m_pulse.X = m_rectangle.x + m_rectangle.w - 10 * CalculateDefiniteFontSize();
                m_pulse.Y = m_faceId.Y;

                m_expression.X = (m_rectangle.x + m_rectangle.w + expressionThreshold >= m_imageWidth)
                    ? (m_rectangle.x - expressionThreshold)
                    : (m_rectangle.x + m_rectangle.w + threshold);
                m_expression.Y = m_rectangle.y + threshold;
            }            
        }
    }
}