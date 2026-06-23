public interface IPID
{
    
    public double Kp { get; set; }
    public double Ki { get; set; }
    public double Kd { get; set; }
    public double ClampMin { get; set; }
    public double ClampMax { get; set; }
    public double AntiWindupMin { get; set; }
    public double AntiWindupMax { get; set; }
    public double SmoothingFactor { get; set; }

    // @Ben I would normally keep these private for a clearer separation of concerns, but I want to be able to debug the controller.
    // Is there a better approach?
    public double PreviousOutput { get; }
    public double Integral { get; }
    public double PreviousError { get; }
    public double ProportionalTerm { get; }
    public double IntegralTerm { get; }
    public double DerivativeTerm { get; }
    public double[] ErrorArray { get; }
    public double[] DeltaTimeArray { get; }

    public double ComputeOutput();
    public void Reset();
    public void Update(double error, double deltaTime);
}