using Avalonia.Threading;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UABEANext3.AssetWorkspace;

public class WorkspaceJobManager
{
    private readonly ConcurrentQueue<IWorkspaceJob> jobQueue;
    private readonly ConcurrentBag<IWorkspaceJob> successfulJobs;
    private readonly ConcurrentBag<IWorkspaceJob> failedJobs;
    private int startingJobCount;
    private int runningJobCount;
    private int maxConcurrentThreads;

    public event EventHandler<string>? JobProgressMessageFired;
    public event EventHandler<float>? ProgressChanged;
    public event EventHandler<bool>? JobsRunning;

    private SemaphoreSlim semaphore;
    private SemaphoreSlim jobsFinishedSemaphore;

    public WorkspaceJobManager(int maxConcurrentThreads = 4)
    {
        jobQueue = new ConcurrentQueue<IWorkspaceJob>();
        successfulJobs = new ConcurrentBag<IWorkspaceJob>();
        failedJobs = new ConcurrentBag<IWorkspaceJob>();
        startingJobCount = 0;
        runningJobCount = 0;
        this.maxConcurrentThreads = maxConcurrentThreads;
        ResetSemaphores();
    }

    private void ResetSemaphores()
    {
        semaphore = new SemaphoreSlim(maxConcurrentThreads);
        jobsFinishedSemaphore = new SemaphoreSlim(0, 1);
    }

    public async Task ProcessJobs(List<IWorkspaceJob> newJobs)
    {
        foreach (var newJob in newJobs)
        {
            jobQueue.Enqueue(newJob);
        }

        JobsRunning?.Invoke(null, true);

        startingJobCount += newJobs.Count;

        Interlocked.Add(ref runningJobCount, jobQueue.Count);

        int initialJobCount = Math.Min(semaphore.CurrentCount, jobQueue.Count);
        for (int i = 0; i < initialJobCount; i++)
        {
            bool dequeueSuccess = jobQueue.TryDequeue(out IWorkspaceJob? job);
            if (!dequeueSuccess || job == null)
            {
                break;
            }

            new Thread(() => RunJob(job)).Start();
        }

        await jobsFinishedSemaphore.WaitAsync();
        ProgressChanged?.Invoke(null, 1f);
        JobsRunning?.Invoke(null, false);
        ResetSemaphores();
        startingJobCount = 0;
        runningJobCount = 0;
    }

    private void RunJob(IWorkspaceJob job)
    {
        bool success;
        try
        {
            success = job.Execute();
        }
        catch
        {
            success = false;
        }

        if (success)
        {
            successfulJobs.Add(job);
        }
        else
        {
            failedJobs.Add(job);
        }

        Interlocked.Decrement(ref runningJobCount);

        if (runningJobCount % 10 == 0 || runningJobCount == 0)
        {
            Dispatcher.UIThread.Post(() =>
            {
                float progressValue = 1f - ((float)runningJobCount / startingJobCount);
                ProgressChanged?.Invoke(null, progressValue);
            }, DispatcherPriority.Background);
        }

        //Dispatcher.UIThread.Post(() =>
        //{
        //    JobProgressMessageFired?.Invoke(null, $"{job.GetTaskName()}... done.");
        //}, DispatcherPriority.ContextIdle);

        bool dequeueSuccess = jobQueue.TryDequeue(out IWorkspaceJob? nextJob);
        if (dequeueSuccess && nextJob != null)
        {
            new Thread(() => RunJob(nextJob)).Start();
        }

        lock (jobsFinishedSemaphore)
        {
            if (runningJobCount == 0 && jobsFinishedSemaphore.CurrentCount == 0)
            {
                jobsFinishedSemaphore.Release();
            }
        }
    }

    public List<IWorkspaceJob> GetSuccessfulJobs()
    {
        return new List<IWorkspaceJob>(successfulJobs);
    }

    public List<IWorkspaceJob> GetFailedJobs()
    {
        return new List<IWorkspaceJob>(failedJobs);
    }
}