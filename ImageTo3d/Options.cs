namespace ImageTo3d
{
    class Options
    {
        public bool Binary = true;
        public bool Negative = false;
        public bool MirrorX = false;
        public bool MirrorY = false;
        public bool AddBorder = true;
        public float DesiredWidth = 100.0f; // In millimeters
        public float MinThickness = 0.5f; // In millimeters
        public float MaxThickness = 3.5f; // In millimeters
        public float BorderThickness = 5.0f;
        public float BorderWidth = 4.0f;
        public float StepSize = 0.2f;
    }
}
