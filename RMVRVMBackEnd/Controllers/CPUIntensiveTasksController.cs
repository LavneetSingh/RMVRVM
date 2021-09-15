using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace RMVRVMBackEnd.Controllers
{
    public class TaskStatus
    {
        public string TaskId { get; set; }
        public string Status { get; set; }
    }
    public class TaskComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            int id1 = Convert.ToInt32(x.Split(' ')[1]);
            int id2 = Convert.ToInt32(y.Split(' ')[1]);
            if (id1 > id2) return -1;
            if (id1 < id2) return 1;
            return 0;
        }
    }

    [Route("api/[controller]")]
    [ApiController]
    public class CPUIntensiveTasks : ControllerBase
    {
        public CPUIntensiveTasks()
        {

        }
        #region API
        [HttpGet("startexperiment")]
        public IActionResult StartExperiment([FromQuery]int iterations)
        {
            lock (syncStartObj)
            {
                var response = new Dictionary<string, string>();
                if (EnableStartButton)
                {
                    Iterations = iterations;
                    EnableStartButton = false;
                    StartSimulation();
                    response.Add("status", "started");
                }
                else
                    response.Add("status", "already started");

                return Ok(JsonSerializer.Serialize(response));
            }
        }
        [HttpGet("stopexperiment")]
        public IActionResult StopExperiment()
        {
            StopSimulation();
            var response = new Dictionary<string, string>();
            response.Add("status", "stopped");
            return Ok(JsonSerializer.Serialize(response));
        }
        [HttpGet("taskstatus")]
        public IActionResult GetTaskStatus()
        {
            TakeLast50Tasks();
            return Ok(JsonSerializer.Serialize(taskStatusVM));
        }
        #endregion

        #region Task Execution
        private static readonly SortedDictionary<string, TaskStatus> taskStatusDictionary = new SortedDictionary<string, TaskStatus>(new TaskComparer());
        private static readonly List<Task> tasks = new List<Task>();
        readonly static object syncStartObj = new object();
        readonly static object syncObj = new object();
        private static readonly List<TaskStatus> taskStatusVM = new List<TaskStatus>();
        private static readonly List<Tuple<string, string>> rateReport = new List<Tuple<string, string>>();
        private static readonly List<Tuple<string, string>> consumptionReport = new List<Tuple<string, string>>();
        private void StopSimulation()
        {
            Task.Run(() => {
                EnableStartButton = true;
            });
        }
        private void TakeLast50Tasks()
        {
            taskStatusDictionary.Clear();
            for (int i = tasks.Count - 1; i >= tasks.Count - 50 && i >= 0; i--)
            {
                taskStatusDictionary["Task " + i] = new TaskStatus { TaskId = "Task " + i, Status = tasks[i].IsCompleted ? "Completed" : "Running" };
            }
            taskStatusVM.Clear();
            taskStatusDictionary.Values.ToList().ForEach(val => taskStatusVM.Add(val));
        }
        private void StartSimulation()
        {
            SetDefaultState();
            Task.Run(() =>
            {
                DateTime start = DateTime.Now;
                for (int i = 0; i < int.MaxValue; i++)
                {
                    lock (syncObj)
                    {
                        tasks.Add(Task.Run(() => CPUIntesiveTask(Iterations)));
                    }
                    Thread.Sleep(1000);
                    Duration = (DateTime.Now - start).ToString(@"hh\:mm\:ss");
                    if (EnableStartButton)
                        break;
                }
            });
        }
        private void SetDefaultState()
        {
            EnableStartButton = false;
            tasks.Clear();
            taskStatusDictionary.Clear();
            taskStatusVM.Clear();
            rateReport.Clear();
            consumptionReport.Clear();
        }

        private static void CPUIntesiveTask(int itr)
        {
            for (int j = 0; j < int.MaxValue / itr; j++)
            {
                Random r = new Random();
                var t = r.NextDouble();
                var art = Math.Tan(t);
                Math.Atan(art);
                Math.Atan2(t, art);
                Math.Atan2(art, t);
            }
        }

        public static string Duration { get; set; }
        public static bool EnableStartButton { get; set; } = true;
        public static int Iterations { get; set; } = 10000;
        #endregion
    }
}
