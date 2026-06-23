public sealed class PID: IPID
{
    public double Kp { get; set; } = 0.0;
    public double Ki { get; set; } = 0.0;
    public double Kd { get; set; } = 0.0;
    public double ClampMin { get; set; } = -1;
    public double ClampMax { get; set; } = 1;
    public double AntiWindupMin { get; set; } = -1e4;
    public double AntiWindupMax { get; set; } = 1e4;
    public double SmoothingFactor { get; set; } = 0; 

    // @Ben I would normally keep these private for a clearer separation of concerns, but I want to be able to debug the controller.
    // Is there a better approach?
    public double PreviousOutput { get; private set; } = 0.0;
    public double Integral { get; private set; } = 0.0;
    public double PreviousError { get; private set; } = 0.0;
    public double ProportionalTerm { get; private set; } = 0.0;
    public double IntegralTerm { get; private set; } = 0.0;
    public double DerivativeTerm { get; private set; } = 0.0;
    public double[] ErrorArray { get; private set; } = new double[3];
    public double[] DeltaTimeArray { get; private set; } = new double[3];

    public PID(double kp = 0.0, double ki = 0.0, double kd = 0.0, double clampMin = -1, double clampMax = 1, double antiWindupMin = -1e4, double antiWindupMax = 1e4, double smoothingFactor = 0)
    {
        Kp = kp;
        Ki = ki;
        Kd = kd;
        ClampMin = clampMin;
        ClampMax = clampMax;
        AntiWindupMin = antiWindupMin;
        AntiWindupMax = antiWindupMax;
        SmoothingFactor = smoothingFactor;
    }

    public double ComputeOutput()
    {
        // Apply clamping to the output of the PID controller
        double output = ProportionalTerm + IntegralTerm + DerivativeTerm;

        // Apply smoothing if SmoothingFactor is greater than 0
        if (SmoothingFactor > 0)
        {
            output = PreviousOutput + SmoothingFactor * (output - PreviousOutput);
            PreviousOutput = output;
        }

        return Math.Clamp(output, ClampMin, ClampMax);
    }

    public void Reset()
    {
        Integral = 0.0;
        PreviousError = 0.0;
        ProportionalTerm = 0.0;
        IntegralTerm = 0.0;
        DerivativeTerm = 0.0;
    }

    public void Update(double error, double deltaTime)
    {
        if (deltaTime <= 0)
        {
            return; // Avoid division by zero or negative time intervals
        }

        Integral += error * deltaTime;

        // Anti-windup: Clamp the integral term to prevent excessive accumulation
        Integral = Math.Clamp(Integral, AntiWindupMin, AntiWindupMax);

        // Put the new error value in the buffer for smoothing
        // TODO: This hurts my soul a bit but I feel the way I would do it in C with a circular buffer and pointer arithmetic would be worse :D
        // @Ben any suggestions on this?
        for (int i = ErrorArray.Length - 1; i > 0; i--)
        {
            ErrorArray[i] = ErrorArray[i - 1];
            DeltaTimeArray[i] = DeltaTimeArray[i - 1];
        }
        ErrorArray[0] = error;
        DeltaTimeArray[0] = deltaTime;

        // Compute centered difference derivative using the buffer
        double derivative = 0.0;
        if (ErrorArray.Length >= 3)
        {
            double dt1 = DeltaTimeArray[0];
            double dt2 = DeltaTimeArray[1];
            double dt3 = DeltaTimeArray[2];

            if (dt1 > 0 && dt2 > 0 && dt3 > 0)
            {
                derivative = (ErrorArray[0] - ErrorArray[2]) / (dt1 + dt2 + dt3);
            }
        }

        // Update the internal terms
        ProportionalTerm = Kp * error;
        IntegralTerm = Ki * Integral;
        DerivativeTerm = Kd * derivative;
    }
}