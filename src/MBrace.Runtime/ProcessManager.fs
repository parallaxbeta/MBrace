﻿module internal Nessos.MBrace.Runtime.Definitions.ProcessManager

open System

open Nessos.Thespian
open Nessos.Thespian.Cluster
open Nessos.Thespian.Cluster.BehaviorExtensions

open Nessos.MBrace
open Nessos.MBrace.Runtime
open Nessos.MBrace.Client
open Nessos.MBrace.Utils
open Nessos.MBrace.Utils.AssemblyCache

let private triggerSytemFault (ctx: BehaviorContext<_>) reply (e: exn) =
    async {
        reply <| Exception (new SystemCorruptedException(e.Message))

        ctx.LogInfo "SYSTEM CORRUPTION DETECTED"
        ctx.LogError e
        
        //TODO!!!
        //Isolate cancellation in this async workflow
        //Deactivating self will cancel the workflow
        try
            ctx.LogInfo "Deactivating process manager..."
            do! Cluster.ClusterManager <!- fun ch -> DeActivateDefinition(ch, { Definition = empDef/"master"/"processManager"; InstanceId = 0 })
        with e -> ctx.LogWarning e

        ctx.LogInfo "TRIGGERING SYSTEM FAILURE..."
        Cluster.ClusterManager <-- FailCluster e
    }

let processManagerBehavior (processMonitor: ActorRef<Replicated<ProcessMonitor, ProcessMonitorDb>>)
                           (assemblyManager: ActorRef<AssemblyManager>)
                           (ctx: BehaviorContext<_>)
                           (msg: ProcessManager) =
    async {
        match msg with
        | ProcessManager.CreateDynamicProcess _ -> return raise <| NotImplementedException()
//        | ProcessManager.CreateDynamicProcess(RR ctx reply as replyChannel, requestId, processImage) ->
//            //ASSUME ALL EXCEPTIONS PROPERLY HANDLED AND DOCUMENTED
//            try
//                //This message may be due to a retry from a client; check what is logged
//                //FaultPoint
//                //SystemCorruptionException => system inconsistency;; SYSTEM FAULT
//                let! processInfoOpt = processMonitor <!- fun ch -> Singular(Choice1Of2 <| TryGetProcessInfoByRequestId(ch, requestId))
//
//                match processInfoOpt with
//                | Some entry -> entry |> Process |> Value |> reply
//                | None ->
//                    //FaultPoint ;; nothing
//                    let! missingAssemblies = assemblyManager <!- fun ch -> CacheAssemblies(ch, processImage.Assemblies)
//
//                    if missingAssemblies.Length <> 0 then
//                        ctx.LogInfo "Assemblies missing. Requesting transmission from client."
//                        missingAssemblies |> MissingAssemblies 
//                                          |> Value 
//                                          |> reply
//                    else
//                        ctx.LogInfo <| sprintf' "Creating new process for (request = %A, client = %A)..."  requestId processImage.ClientId
//
//                        let assemblyIds = processImage.Assemblies |> Array.map (function packet -> packet.Header)
//
//                        let processInitializationRecord: ProcessCreationData = { 
//                            ClientId = processImage.ClientId
//                            RequestId = requestId
//                            Name = processImage.Name
//                            Type = processImage.Type
//                            TypeName = processImage.TypeName
//                            Dependencies = assemblyIds
//                        }
//
//                        //FaultPoint
//                        //SystemException => run out of pid slots ;; SYSTEM FAULT
//                        //TODO!!! Maybe not treat this as a system fault
//                        let! processRecord = processMonitor <!- fun ch -> Singular(Choice1Of2 <| InitializeProcess(ch, processInitializationRecord))
//
//                        //FaultPoint
//                        //BroadcastFailureException => failed to do any kind of replication ;; SYSTEM FAULT
//                        do! processMonitor <!- fun ch -> Replicated(ch, Choice1Of2 <| NotifyProcessInitialized processRecord)
//
//                        ctx.LogInfo <| sprintf' "Initialized new cloud process %A" processRecord.ProcessId
//
//                        let processActivationRecord = {
//                            Definition = empDef/"process"
//                            Instance = Some processRecord.ProcessId
//                            Configuration = ActivationConfiguration.FromValues [Conf.Val Configuration.RequiredAssemblies assemblyIds]
//                            ActivationStrategy = ActivationStrategy.processStrategy
//                        }
//
//                        //FaultPoint
//                        //InvalidActivationStrategy => invalid argument;; collocation strategy not supported ;; SYSTEM FAULT
//                        //CyclicDefininitionDependency => invalid configuration ;; SYSTEM FAULT
//                        //OutOfNodesExceptions => unable to recover due to lack of nodes ;; SYSTEM FAULT
//                        //KeyNotFoundException ;; from NodeManager => definition.Path not found in node;; SYSTEM FAULT
//                        //ActivationFailureException ;; from NodeManager => failed to activate definition.Path;; reply exception
//                        //SystemCorruptionException => system failure occurred during execution;; SYSTEM FAULT
//                        //SystemFailureException => Global system failure;; SYSTEM FAULT
//                        do! Cluster.ClusterManager <!- fun ch -> ActivateDefinition(ch, processActivationRecord)
//
//                        ctx.LogInfo <| sprintf' "Cloud process %A is ready..." processRecord.ProcessId
//
//                        //FaultPoint
//                        //SystemCorruptionException => system inconsistency;; SYSTEM FAULT
//                        let! r = Cluster.ClusterManager <!- fun ch -> ResolveActivationRefs(ch, { Definition = empDef/"process"/"scheduler"/"schedulerRaw"; InstanceId = processRecord.ProcessId })
//                        //Throws
//                        //InvalidCastException => SYSTEM FAULT
//                        let scheduler = new RawActorRef<Scheduler>(r.[0] :?> ActorRef<RawProxy>)
//
//                        ctx.LogInfo "Scheduler resolved."
//
//                        //FaultPoint
//                        //BroadcastFailureException => no replication at all;; root task not posted;; SYSTEM FAULT
//                        do! scheduler <!- fun ch -> NewProcess(ch, processRecord.ProcessId, processImage.Computation)
//                        
//                        //FaultPoint
//                        //BroadcastFailureException => no replication at all;; SYSTEM FAULT
//                        do! processMonitor <!- fun ch -> Replicated(ch, Choice1Of2 <| NotifyProcessStarted(processRecord.ProcessId, System.DateTime.Now))
//
//                        ctx.LogInfo <| sprintf "Cloud process %A is running..." processRecord.ProcessId
//
//                        //FaultPoint
//                        //SystemCorruptionException => system inconsistency;; SYSTEM FAULT
//                        let! processInfo = processMonitor <!- fun ch -> Singular(Choice1Of2 <| TryGetProcessInfo(ch, processRecord.ProcessId))
//
//                        processInfo |> Option.get |> Process |> Value |> reply
//            with MessageHandlingException2(ActivationFailureException _ as e) ->
//                    reply <| Exception (new MBraceException(sprintf "Failed to activate process: %s" e.Message))
//                | MessageHandlingException2(SystemFailureException _ as e) ->
//                    reply <| Exception (new SystemFailedException(e.Message))
//                | MessageHandlingException2 e ->
//                    do! triggerSytemFault ctx reply e
//                | :? InvalidCastException as e ->
//                    do! triggerSytemFault ctx reply e
//                | BroadcastFailureException _ as e ->
//                    do! triggerSytemFault ctx reply e
//                | e ->
//                    do! triggerSytemFault ctx reply e

        | ProcessManager.RequestDependencies(RR ctx reply, missing) ->
            try
                //FaultPoint
                //-
                let! images = assemblyManager <!- fun ch -> AssemblyManager.GetImages(ch, Some missing)

                images |> Value |> reply
            with e ->
                ctx.LogError e
                reply (Exception e)

        | ProcessManager.GetProcessInfo(RR ctx reply, processId) ->
            let! processInfo = processMonitor <!- fun ch -> Singular(Choice1Of2 <| TryGetProcessInfo(ch, processId))
            match processInfo with
            | None -> SystemException("Could not retrieve process info entry.") |> Reply.exn |> reply
            | Some i -> i |> Value |> reply

        | ProcessManager.GetAllProcessInfo replyChan ->
            //TODO!!! If there is a repication failure, the client will see it?
            processMonitor <-- Singular(Choice1Of2 <| ProcessMonitor.GetAllProcessInfo replyChan)

        | ProcessManager.KillProcess(RR ctx reply, processId) ->
            try
                //FaultPoint
                //-
                //do! processMonitor <!- fun ch -> Replicated(ch, Choice1Of2 <| CompleteProcess(processId, ExceptionResult (new ProcessKilledException("Process has been killed.") :> exn, None) |> Serializer.Serialize |> ProcessSuccess))
                do! processMonitor <!- fun ch -> Replicated(ch, Choice1Of2 <| CompleteProcess(processId, ProcessKilled))

                //FaultPoint
                //-
                do! Cluster.ClusterManager <!- fun ch -> DeActivateDefinition(ch, { Definition = empDef/"process"; InstanceId = processId })

                reply nothing
            with e ->
                ctx.LogError e
                reply (Exception e)

        | ProcessManager.ClearProcessInfo(replyChannel, processId) ->
            //TODO!!! If there is a repication failure, the client will see it?
            processMonitor <-- Replicated(replyChannel, Choice1Of2 <| FreeProcess processId)

        | ProcessManager.ClearAllProcessInfo replyChannel ->
            //TODO!!! If there is a repication failure, the client will see it?
            processMonitor <-- Replicated(replyChannel, Choice1Of2 FreeAllProcesses)
    }
