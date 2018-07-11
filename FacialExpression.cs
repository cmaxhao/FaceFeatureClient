using DF_FaceTracking.cs.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DF_FaceTracking.cs
{
    public  class FacialExpression
    {
        //{
        //    EXPRESSION_BROW_RAISER_LEFT = 0,
        //    EXPRESSION_BROW_RAISER_RIGHT = 1,
        //    EXPRESSION_BROW_LOWERER_LEFT = 2,
        //    EXPRESSION_BROW_LOWERER_RIGHT = 3,
        //    EXPRESSION_SMILE = 4,
        //    EXPRESSION_KISS = 5,
        //    EXPRESSION_MOUTH_OPEN = 6,
        //    EXPRESSION_EYES_CLOSED_LEFT = 7,
        //    EXPRESSION_EYES_CLOSED_RIGHT = 8,
        //    EXPRESSION_HEAD_TURN_LEFT = 9,
        //    EXPRESSION_HEAD_TURN_RIGHT = 10,
        //    EXPRESSION_HEAD_UP = 11,
        //    EXPRESSION_HEAD_DOWN = 12,
        //    EXPRESSION_HEAD_TILT_LEFT = 13,
        //    EXPRESSION_HEAD_TILT_RIGHT = 14,
        //    EXPRESSION_EYES_TURN_LEFT = 15,
        //    EXPRESSION_EYES_TURN_RIGHT = 16,
        //    EXPRESSION_EYES_UP = 17,
        //    EXPRESSION_EYES_DOWN = 18,
        //    EXPRESSION_TONGUE_OUT = 19,
        //    EXPRESSION_PUFF_RIGHT = 20,
        //    EXPRESSION_PUFF_LEFT = 21
        //}

        // RealSense提供，包含表情及程度
        public int[] facialExpressionIndensity { set; get; }
        // 表情发生时间
        public CustomTime happenTS { set; get; }

        public FacialExpression()
        {
            this.facialExpressionIndensity = new int[22];
        }
    }
}
