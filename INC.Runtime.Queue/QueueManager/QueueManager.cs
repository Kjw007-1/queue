﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.Concurrent;
using System.Threading;

namespace INC.Runtime.Queue
{
    public class QueueManager : IQueueManager, IDisposable
    {
        private JobContainer jobContainer;
        private QueueTaskContainer queueTaskContainer;
        private readonly IQueueConfirguration confirguration;
        private readonly IQueueTaskConfiguration queueTaskConfiguration;
        private QueueState state = QueueState.New;
        private int taskJobCount = 0;

        public QueueManager(IQueueConfirguration confirguration)
        {
            this.jobContainer = new JobContainer();
            this.queueTaskContainer = new QueueTaskContainer(confirguration.TaskMaxCount);
            this.confirguration = confirguration;
            this.queueTaskConfiguration = QueueTaskConfiguration.GetConfiguration(this.confirguration);
        }

        /// <summary>
        /// Queue Manager state
        /// </summary>
		public QueueState State { get { return state; } private set { this.state = value; } }


        /// <summary>
        /// Try to create new task to run job 
        /// </summary>
        private IQueueTask InvokeTask()
        {
            if (jobContainer.Count - taskJobCount > 0)
            {
                var task = this.queueTaskContainer.CreateNewTask(this.queueTaskConfiguration);
                if (task != null)
                {
                    task.OnTaskBegin += QueueTask_OnTaskBegin;
                    task.OnTaskJobBegin += QueueTask_OnTaskJobBegin;
                    task.OnTaskJobComplete += QueueTask_OnJobComplete;
                    task.OnTaskWakeUp += QueueTask_OnTaskWakeUp;
                    task.OnTaskComplete += QueueTask_OnTaskComplete;
                    task.Run();
                }

                Interlocked.Increment(ref taskJobCount);
                return task;
            }
            else
                return null;
        }

        #region Queue Task Event

        /// <summary>
        /// task start event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        protected void QueueTask_OnTaskBegin(object sender, EventArgs args)
        {
            var task = sender as IQueueTask;
            task.CurrentJob = this.jobContainer.GetJob();
            Interlocked.Decrement(ref taskJobCount);
        }

        protected void QueueTask_OnTaskJobBegin(object sender, Delegate.QueueTaskEventArgs args)
        {
        }

        /// <summary>
        /// task job complete event 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        protected void QueueTask_OnJobComplete(object sender, Delegate.QueueTaskEventArgs args)
        {
            var task = sender as IQueueTask;
            if (task.CurrentJob != null && task.CurrentJob.CompleteCallback != null)
                task.CurrentJob.CompleteCallback(task.CurrentJob);

            //Get The Last job
            task.CurrentJob = this.jobContainer.GetJob();
        }


        /// <summary>
        ///  task complete event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        protected void QueueTask_OnTaskComplete(object sender, EventArgs args)
        {
            var task = sender as IQueueTask;
            this.queueTaskContainer.RemomveTask(task);
            if (this.jobContainer.Count > 0)
                InvokeTask();
        }


        protected bool QueueTask_OnTaskWakeUp(object sender, EventArgs args)
        {
            if (jobContainer.Count > 0)
                return true;
            else
                return false;
        }

        #endregion

        #region Public Method

        /// <summary>
        ///  add a job 
        /// </summary>
        /// <param name="job"></param>
        public void AddJob(JobBase job)
        {
            this.jobContainer.AddJob(job);
            if (State == QueueState.Started)
            {
                InvokeTask();
            }
        }

        public void Start()
        {
            this.State = QueueState.Starting;
            IQueueTask task;
            do
            {
                task = InvokeTask();
            } while (task != null);

            this.State = QueueState.Started;
        }

        public void Stop()
        {
            this.State = QueueState.Stopped;
        }
        #endregion


        #region Implement IDisposable
        public void Dispose()
        {
            this.jobContainer.Dispose();
            this.queueTaskContainer.Dispose();
        }
        #endregion
    }
}
