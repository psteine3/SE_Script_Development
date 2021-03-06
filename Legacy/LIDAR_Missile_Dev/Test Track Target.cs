
// LIDAR Tracking Variables

double maxRaycastDistance = 2500; // Max raycast range the missile is allowed to use (meters)
double raycastOvershootDistance = 100; // Raycast overshoots target by this many meters
double uncertantyPerSecond = 7.5; // Amount that target is expected to deviate from its current course per second (meters)
double LIDARGridSize = 4; // LIDAR grid search pattern size (meters)
int cameraIndex = 0; // Index for which camera to use (cycles through all cameras)

List<IMyCameraBlock> RaycastCameras = new List<IMyCameraBlock>();

MyDetectedEntityInfo LatestDetectedEntityInfo;

Vector3D LastKnownPosition;
Vector3D TargetOffsetFromCenter;
Vector3D LastKnownVelocity;
double secondsSinceLastLIDARUpdate = 0;
int cycleCounter = 0;


public Program() {
    Runtime.UpdateFrequency = UpdateFrequency.Update10; // Update at 6Hz

    TargetOffsetFromCenter = Vector3D.Zero; // Set to no offset for testing
    LastKnownVelocity = Vector3D.Zero; // Set to 0,0,0 for testing
    LastKnownPosition = new Vector3D(269.18f,176.96f,89.21f);
}

void Main(string arg) {
    // Do this first
    secondsSinceLastLIDARUpdate += 0.160;
    Echo(secondsSinceLastLIDARUpdate.ToString("0.000"));

    // =================================

    if (TrackTarget()) {
        Echo("\n" + LatestDetectedEntityInfo.Name);
    }
}

bool TrackTarget() {
    if (cycleCounter == 1) { // Update every 1 cycles (3Hz)
        cycleCounter = 0; // Reset cycleCounter

        Vector3D EstimatedPosition = CalculateTargetPosition(LastKnownPosition, LastKnownVelocity, secondsSinceLastLIDARUpdate);
        if (RaycastAtPoint(EstimatedPosition, secondsSinceLastLIDARUpdate, out LatestDetectedEntityInfo)) {
            // Raycast was successful, update information on target
            LastKnownPosition = LatestDetectedEntityInfo.Position + TargetOffsetFromCenter;
            LastKnownVelocity = new Vector3D(LatestDetectedEntityInfo.Velocity);
            secondsSinceLastLIDARUpdate = 0; // Updated
            return true; // Tracking was successful, return true
        }
        else {
            // Raycast did not hit anything, try a LIDAR Scan
            if (CastLIDARScan(EstimatedPosition, secondsSinceLastLIDARUpdate, out LatestDetectedEntityInfo)) {
                // LIDAR scan was successful, update information on target
                LastKnownPosition = LatestDetectedEntityInfo.Position + TargetOffsetFromCenter;
                LastKnownVelocity = new Vector3D(LatestDetectedEntityInfo.Velocity);
                secondsSinceLastLIDARUpdate = 0; // Updated
                return true; // Tracking was successful, return true
            }
            else {
                return false; // LIDAR scan did not hit anything, return false since Tracking failed
            }
        }
    }
    else {
        cycleCounter++; // Increment cycleCounter
    }
    return false; // If you get here then tracking failed
}

bool CastLIDARScan(Vector3D target, double secondsSinceLIDARUpdate, out MyDetectedEntityInfo RaycastResult) { // Casts a LIDAR scan with uncertainty given by the time since last good ping
    GridTerminalSystem.GetBlocksOfType(RaycastCameras); // Get all cameras on our entity (All cameras should be oriented forward so lets not check that)

    // for (int i = 0; i < RaycastCameras.Count; i++) {
    //     RaycastCameras[i].EnableRaycast = true;
    // }

    Vector3D VectorToTarget = target - Me.GetPosition(); // Get vector from current position to target position
    double DistanceToTarget = VectorToTarget.Length(); // Get distance to target
    double uncertainty = (double)secondsSinceLIDARUpdate * uncertantyPerSecond; // Get meters of uncertainty from target

    Vector3D BaseVector;

    if ((DistanceToTarget + raycastOvershootDistance) > maxRaycastDistance) {
        BaseVector = Vector3D.Normalize(VectorToTarget) * maxRaycastDistance; // Vector with Max Length because object is most likely at max range
    }
    else {
        BaseVector = Vector3D.Normalize(VectorToTarget) * (DistanceToTarget + raycastOvershootDistance); // Vector to target overshooting by raycastOvershootDistance
    }

    RaycastResult = RaycastCameras[cameraIndex].Raycast(BaseVector + Me.GetPosition()); // Raycast Forwards
    if (!RaycastResult.IsEmpty()) {
        return true; // Raycast hit something, terminate and return true
    }
    cameraIndex = IterateIndex(cameraIndex, RaycastCameras.Count); // Iterate camera index to use the next camera

    Quaternion TransformationQuat;
    Vector3D VectorToSectorPoint;

    // Iterate through pitch axis values for raycasting (yaw = 0)
    for (int PitchIteration = 1; PitchIteration < Math.Ceiling(uncertainty/LIDARGridSize); PitchIteration ++) {
        double PitchAngleOffset = Math.Atan2(LIDARGridSize * PitchIteration, DistanceToTarget);

        TransformationQuat = Quaternion.CreateFromYawPitchRoll(0f, (float)PitchAngleOffset, 0f); // Create transformation quaternion for positive axis
        VectorToSectorPoint = Vector3D.Transform(BaseVector, TransformationQuat);
        RaycastResult = RaycastCameras[cameraIndex].Raycast(VectorToSectorPoint + Me.GetPosition());
        cameraIndex = IterateIndex(cameraIndex, RaycastCameras.Count); // Iterate camera index to use the next camera
        if (!RaycastResult.IsEmpty()) {
            return true; // Raycast hit something, terminate and return true
        }

        TransformationQuat = Quaternion.CreateFromYawPitchRoll(0f, -(float)PitchAngleOffset, 0f); // Create transformation quaternion for negative axis
        VectorToSectorPoint = Vector3D.Transform(BaseVector, TransformationQuat);
        RaycastResult = RaycastCameras[cameraIndex].Raycast(VectorToSectorPoint + Me.GetPosition());
        cameraIndex = IterateIndex(cameraIndex, RaycastCameras.Count); // Iterate camera index to use the next camera
        if (!RaycastResult.IsEmpty()) {
            return true; // Raycast hit something, terminate and return true
        }
        
    }

    // Iterate through yaw axis values for raycasting (pitch = 0)
    for (int YawIteration = 1; YawIteration < Math.Ceiling(uncertainty/LIDARGridSize); YawIteration ++) {
        double YawAngleOffset = Math.Atan2(LIDARGridSize * YawIteration, DistanceToTarget);

        TransformationQuat = Quaternion.CreateFromYawPitchRoll((float)YawAngleOffset, 0f, 0f); // Create transformation quaternion for positive axis
        VectorToSectorPoint = Vector3D.Transform(BaseVector, TransformationQuat);
        RaycastResult = RaycastCameras[cameraIndex].Raycast(VectorToSectorPoint + Me.GetPosition());
        cameraIndex = IterateIndex(cameraIndex, RaycastCameras.Count); // Iterate camera index to use the next camera
        if (!RaycastResult.IsEmpty()) {
            return true; // Raycast hit something, terminate and return true
        }

        TransformationQuat = Quaternion.CreateFromYawPitchRoll(-(float)YawAngleOffset, 0f, 0f); // Create transformation quaternion for negative axis
        VectorToSectorPoint = Vector3D.Transform(BaseVector, TransformationQuat);
        RaycastResult = RaycastCameras[cameraIndex].Raycast(VectorToSectorPoint + Me.GetPosition());
        cameraIndex = IterateIndex(cameraIndex, RaycastCameras.Count); // Iterate camera index to use the next camera
        if (!RaycastResult.IsEmpty()) {
            return true; // Raycast hit something, terminate and return true
        }
        
    }

    // Iterate through all non axis values for raycasting
    // Start from iteration 1 because we already raycasted directly towards the target position
    for (int PitchIteration = 1; PitchIteration < Math.Ceiling(uncertainty/LIDARGridSize); PitchIteration ++) {
        for (int YawIteration = 1; YawIteration < Math.Ceiling(uncertainty/LIDARGridSize); YawIteration ++) {

            // Get offset angles in radiens for the transformation quaternion
            double PitchAngleOffset = Math.Atan2(LIDARGridSize * PitchIteration, DistanceToTarget);
            double YawAngleOffset = Math.Atan2(LIDARGridSize * YawIteration, DistanceToTarget);

            TransformationQuat = Quaternion.CreateFromYawPitchRoll((float)YawAngleOffset, (float)PitchAngleOffset, 0f); // Create transformation quaternion for sector 1
            VectorToSectorPoint = Vector3D.Transform(BaseVector, TransformationQuat);
            RaycastResult = RaycastCameras[cameraIndex].Raycast(VectorToSectorPoint + Me.GetPosition());
            cameraIndex = IterateIndex(cameraIndex, RaycastCameras.Count); // Iterate camera index to use the next camera
            if (!RaycastResult.IsEmpty()) {
                return true; // Raycast hit something, terminate and return true
            }


            TransformationQuat = Quaternion.CreateFromYawPitchRoll(-(float)YawAngleOffset, (float)PitchAngleOffset, 0f); // Create transformation quaternion for sector 2
            VectorToSectorPoint = Vector3D.Transform(BaseVector, TransformationQuat);
            RaycastResult = RaycastCameras[cameraIndex].Raycast(VectorToSectorPoint + Me.GetPosition());
            cameraIndex = IterateIndex(cameraIndex, RaycastCameras.Count); // Iterate camera index to use the next camera
            if (!RaycastResult.IsEmpty()) {
                return true; // Raycast hit something, terminate and return true
            }


            TransformationQuat = Quaternion.CreateFromYawPitchRoll(-(float)YawAngleOffset, -(float)PitchAngleOffset, 0f); // Create transformation quaternion for sector 3
            VectorToSectorPoint = Vector3D.Transform(BaseVector, TransformationQuat);
            RaycastResult = RaycastCameras[cameraIndex].Raycast(VectorToSectorPoint + Me.GetPosition());
            cameraIndex = IterateIndex(cameraIndex, RaycastCameras.Count); // Iterate camera index to use the next camera
            if (!RaycastResult.IsEmpty()) {
                return true; // Raycast hit something, terminate and return true
            }


            TransformationQuat = Quaternion.CreateFromYawPitchRoll((float)YawAngleOffset, -(float)PitchAngleOffset, 0f); // Create transformation quaternion for sector 4
            VectorToSectorPoint = Vector3D.Transform(BaseVector, TransformationQuat);
            RaycastResult = RaycastCameras[cameraIndex].Raycast(VectorToSectorPoint + Me.GetPosition());
            cameraIndex = IterateIndex(cameraIndex, RaycastCameras.Count); // Iterate camera index to use the next camera
            if (!RaycastResult.IsEmpty()) {
                return true; // Raycast hit something, terminate and return true
            }
            

        }
    }

    return false; // Raycasts didn't hit anything, return false
}

bool RaycastAtPoint(Vector3D target, double secondsSinceLIDARUpdate, out MyDetectedEntityInfo RaycastResult) {
    GridTerminalSystem.GetBlocksOfType(RaycastCameras); // Get all cameras on our entity (All cameras should be oriented forward so lets not check that)

    Vector3D VectorToTarget = target - Me.GetPosition(); // Get vector from current position to target position
    double DistanceToTarget = VectorToTarget.Length(); // Get distance to target

    Vector3D BaseVector;

    if ((DistanceToTarget + raycastOvershootDistance) > maxRaycastDistance) {
        BaseVector = Vector3D.Normalize(VectorToTarget) * maxRaycastDistance; // Vector with Max Length because object is most likely at max range
    }
    else {
        BaseVector = Vector3D.Normalize(VectorToTarget) * (DistanceToTarget + raycastOvershootDistance); // Vector to target overshooting by raycastOvershootDistance
    }

    RaycastResult = RaycastCameras[cameraIndex].Raycast(BaseVector + Me.GetPosition()); // Raycast Forwards
    cameraIndex = IterateIndex(cameraIndex, RaycastCameras.Count); // Iterate camera index to use the next camera
    if (!RaycastResult.IsEmpty()) {
        return true; // Raycast hit something, terminate and return true
    }
    return false; // Forward Raycast didn't hit anything, terminate and return false
}

int IterateIndex(int CurrentValue, int NumIndexes) { // Increments given index by one looping back to 0 at NumberIndexes
    CurrentValue ++;
    if (CurrentValue == NumIndexes) {
        return 0; // Loop back to index 0
    }
    return CurrentValue; // Otherwise return the value plus 1
}

Vector3D CalculateTargetPosition(Vector3D LastPosition, Vector3D LastVelocity, double secondsSinceLIDARUpdate) {
    return LastPosition + (LastVelocity * secondsSinceLIDARUpdate);
}