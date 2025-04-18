using UnityEngine;
using Unity.Barracuda;
using System.Collections.Generic;

public class InferenceManager : MonoBehaviour
{
    public NNModel treadmillIntensityModel;
    public NNModel ellipticalIntensityModel;
    public NNModel rowIntensityModel;
    public NNModel machineModel;

    private Model runtimeModel;
    private IWorker worker;

    void Start()
    {
        runtimeModel = ModelLoader.Load(treadmillIntensityModel);
        worker = WorkerFactory.CreateWorker(runtimeModel);
    }

    public void RunInference(float[] features)
    {
        Tensor inputTensor = new Tensor(1, features.Length, features);

        Tensor outputTensor = worker.Execute(inputTensor).PeekOutput();

        float[] results = outputTensor.ToReadOnlyArray();
        Debug.Log("Inference Output: " + string.Join(", ", results));

        inputTensor.Dispose();
        outputTensor.Dispose();
    }

    void OnDestroy()
    {
        worker.Dispose();
    }
}