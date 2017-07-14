using System;
using System.Collections.Generic;
using System.Linq;

namespace Dagmatic.Akka.Actor
{
    public class GT<TData> : MachineHost
    {
        protected GoalMachine Machine { get; set; }

        protected override void OnReceive(object message)
        {
            Machine.ProcessMessage(message);
        }

        protected GoalMachine.ActionG Execute(Action<GoalMachine.IContext> action)
            => new GoalMachine.ActionG(action);

        protected GoalMachine.IfG If(GoalMachine.IGoal cond, GoalMachine.IGoal @then = null, GoalMachine.IGoal @else = null)
            => new GoalMachine.IfG(cond, @then, @else);

        protected GoalMachine.IfG If(Func<GoalMachine.IContext, bool> pred, GoalMachine.IGoal @then = null, GoalMachine.IGoal @else = null)
            => new GoalMachine.IfG(Condition(pred), @then, @else);

        protected GoalMachine.ConditionG Condition(Func<GoalMachine.IContext, bool> pred)
            => new GoalMachine.ConditionG(pred);

        protected GoalMachine.WhenG When(Func<GoalMachine.IContext, bool> pred)
            => new GoalMachine.WhenG(pred);

        protected GoalMachine.IGoal ReceiveAny(GoalMachine.IGoal child = null)
            => And(When(ctx => ctx.CurrentMessage != null), child ?? Ok());

        protected GoalMachine.AndG And(GoalMachine.IGoal goal1, GoalMachine.IGoal goal2)
            => new GoalMachine.AndG(goal1, goal2);

        protected GoalMachine.IGoal ReceiveAny(Func<object, bool> shouldHandle, GoalMachine.IGoal child = null)
            => And(When(ctx => ctx.CurrentMessage != null && shouldHandle(ctx.CurrentMessage)), child ?? Ok());

        protected GoalMachine.IGoal Receive<T>(GoalMachine.IGoal child = null)
            => ReceiveAny(msg => msg is T, child);

        protected GoalMachine.IGoal Receive<T>(Func<T, bool> shouldHandle, GoalMachine.IGoal child = null)
            => ReceiveAny(msg => msg is T && shouldHandle((T)msg), child);

        protected GoalMachine.SequenceG Sequence(Func<IEnumerable<GoalStatus>, GoalStatus> pred, params GoalMachine.IGoal[] children)
            => new GoalMachine.SequenceG(pred, children);

        protected GoalMachine.ParallelG Parallel(Func<IEnumerable<GoalStatus>, GoalStatus> pred, params GoalMachine.IGoal[] children)
            => new GoalMachine.ParallelG(pred, children);

        protected GoalMachine.SequenceG AnySucceed(params GoalMachine.IGoal[] children)
            => Sequence(GoalEx.AnySucceed, children);

        protected GoalMachine.SequenceG AllSucceed(params GoalMachine.IGoal[] children)
            => Sequence(GoalEx.AllSucceed, children);

        protected GoalMachine.SequenceG AllComplete(params GoalMachine.IGoal[] children)
            => Sequence(GoalEx.AllComplete, children);

        protected GoalMachine.WhileActionG Loop(Action<GoalMachine.IContext> action)
            => While(_ => true, action);

        protected GoalMachine.UntilG Loop(Func<GoalMachine.IContext, GoalMachine.IGoal> body)
            => Until(When(_ => false), body);

        protected GoalMachine.UntilG Forever(Func<GoalMachine.IContext, GoalMachine.IGoal> body)
            => Until(When(_ => false), body, false);

        protected GoalMachine.UntilG While(Func<GoalMachine.IContext, bool> pred, Func<GoalMachine.IContext, GoalMachine.IGoal> body)
            => Until(When(ctx => !pred(ctx)), body);

        protected GoalMachine.WhileActionG Forever(Action<GoalMachine.IContext> action)
            => While(_ => true, action, false);

        protected GoalMachine.WhileActionG While(Func<GoalMachine.IContext, bool> pred, Action<GoalMachine.IContext> action, bool failOnException = true)
            => new GoalMachine.WhileActionG(pred, action);

        protected GoalMachine.UntilG Until(GoalMachine.IGoal pred, Func<GoalMachine.IContext, GoalMachine.IGoal> body, bool failOnBodyFailure = true)
            => new GoalMachine.UntilG(pred, body, failOnBodyFailure);

        protected GoalMachine.NotG Not(GoalMachine.IGoal child)
            => new GoalMachine.NotG(child);

        protected GoalMachine.IGoal Pass(GoalMachine.IGoal child)
            => Then(child, Ok());

        protected GoalMachine.FailG Fail()
            => new GoalMachine.FailG();

        protected GoalMachine.IGoal After(GoalMachine.IGoal child, GoalMachine.IGoal after)
            => Then(child, after);

        protected GoalMachine.NextMessageG NextMessage(GoalMachine.IGoal goal, Func<GoalMachine.IContext, bool> pred = null)
            => new GoalMachine.NextMessageG(goal, pred);

        protected GoalMachine.NextMessageG NextMessage(Func<object, bool> pred, GoalMachine.IGoal goal)
            => NextMessage(goal, ctx => pred(ctx.CurrentMessage));

        protected GoalMachine.NextMessageG NextMessage<T>(GoalMachine.IGoal goal)
            => NextMessage(goal, ctx => ctx.CurrentMessage is T);

        protected GoalMachine.NextMessageG NextMessage<T>(Func<T, bool> pred, GoalMachine.IGoal goal)
            => NextMessage(goal, ctx => ctx.CurrentMessage is T && pred((T)ctx.CurrentMessage));

        protected GoalMachine.ThenG Then(GoalMachine.IGoal child, GoalMachine.IGoal then)
            => new GoalMachine.ThenG(child, then);

        protected GoalMachine.BecomeG Become(Func<GoalMachine.IGoal> factory)
            => new GoalMachine.BecomeG(factory);

        protected GoalMachine.SpawnG Spawn(GoalMachine.IGoal child)
            => new GoalMachine.SpawnG(child);

        protected GoalMachine.TimeoutG Timeout(TimeSpan delay, GoalMachine.IGoal child, GoalMachine.IGoal onTimeout = null)
            => new GoalMachine.TimeoutG(_ => delay, child, onTimeout ?? Fail());

        protected GoalMachine.TimeoutG Timeout(Func<GoalMachine.IContext, TimeSpan> delay, GoalMachine.IGoal child, GoalMachine.IGoal onTimeout = null)
            => new GoalMachine.TimeoutG(delay, child, onTimeout ?? Fail());

        protected GoalMachine.NeverG Never()
            => new GoalMachine.NeverG();

        protected GoalMachine.IGoal Delay(TimeSpan delay, GoalMachine.IGoal after = null)
            => Delay(_ => delay, after ?? Ok());

        protected GoalMachine.IGoal Delay(Func<GoalMachine.IContext, TimeSpan> delay, GoalMachine.IGoal after = null)
            => new GoalMachine.TimeoutG(delay, Never(), after ?? Ok());

        protected GoalMachine.OkG Ok()
            => new GoalMachine.OkG();


        protected void StartWith(GoalMachine.IGoal goal, TData data)
        {
            Machine = new GoalMachine(data, this);
            Machine.Run(goal);
        }

        public class GoalMachine
        {
            private List<GoalWrapper> _goals = new List<GoalWrapper>();

            public GoalMachine(TData data, IMachineHost host)
            {
                Data = data;
                Host = host;
            }

            public TData Data { get; }
            public IMachineHost Host { get; }
            public object CurrentMessage { get; private set; }
            public ulong MessageCount { get; private set; }

            public void ProcessMessage(object message)
            {
                MessageCount++;
                CurrentMessage = message;

                foreach (var goal in _goals.ToList())
                {
                    RunWrapper(goal);
                }
            }

            public void Run(IGoal goal)
            {
                var wrapper = new GoalWrapper(goal);
                _goals.Add(wrapper);

                RunWrapper(wrapper);
            }

            private void RunWrapper(GoalWrapper goal)
            {
                var ctx = new Context(CurrentMessage, Data, Host, MessageCount);

                goal.Next(ctx);

                if (goal.Status == GoalStatus.Pending)
                    return;

                if (goal.Status == GoalStatus.Failure)
                    Failed?.Invoke(this, goal);

                _goals.Remove(goal);
            }

            public event EventHandler<IGoal> Failed;

            private class GoalWrapper : GoalBase
            {
                public IGoal Goal;
                private GoalWrapper _root;

                public GoalWrapper(IGoal goal, GoalWrapper root = null)
                {
                    Goal = goal;
                    _root = root ?? this;
                    Status = goal.Status;
                }

                private GoalWrapper Become(Func<IGoal> factory)
                {
                    if (this == _root)
                    {
                        Goal = factory();
                        Status = Goal.Status;

                        return this;
                    }
                    else
                    {
                        Status = GoalStatus.Success;
                        return _root.Become(factory);
                    }
                }

                public override IGoal Next(IContext ctx)
                {
                    if (Status == GoalStatus.Pending)
                    {
                        var curr = Goal;
                        while (Goal.Status == GoalStatus.Pending && curr != null)
                        {
                            try
                            {
                                Goal = curr;
                                curr = curr.Next(ctx);

                                var become = Goal as BecomeG;
                                if (become != null)
                                {
                                    var wrapper = Become(become.Factory);
                                    if (wrapper != this)
                                    {
                                        wrapper.Next(ctx);
                                    }
                                }

                                var spawn = Goal as SpawnG;
                                if (spawn != null)
                                {
                                    Goal = new GoalWrapper(spawn.Child);
                                    curr = Goal;
                                }
                            }
                            catch (Exception ex)
                            {
                                Goal = new ExceptionG(ex, Goal);
                            }
                        }

                        Status = Goal.Status;
                    }

                    return null;
                }
            }

            public interface IContext
            {
                object CurrentMessage { get; }

                TData Data { get; }

                IMachineHost Host { get; }
                ulong MessageCount { get; }
            }

            public struct Context : IContext
            {
                public Context(object message, TData data, IMachineHost host, ulong messageCount)
                {
                    CurrentMessage = message;
                    Data = data;
                    Host = host;
                    MessageCount = messageCount;
                }

                public object CurrentMessage { get; }

                public TData Data { get; }

                public IMachineHost Host { get; }
                public ulong MessageCount { get; }
            }

            public interface IGoal
            {
                GoalStatus Status { get; }

                IGoal Next(IContext ctx);

                object FailureReason { get; }
            }

            public class BecomeG : GoalBase
            {
                public BecomeG(Func<IGoal> factory)
                {
                    Factory = factory;
                }

                public Func<IGoal> Factory { get; }

                public override GoalStatus Status => GoalStatus.Pending;
            }

            public class SpawnG : GoalBase
            {
                public SpawnG(IGoal child)
                {
                    Child = child;
                }

                public IGoal Child { get; }
            }

            public class GoalBase : IGoal
            {
                public virtual object FailureReason { get; protected set; }

                public virtual GoalStatus Status { get; protected set; }

                public virtual IGoal Next(IContext ctx)
                {
                    return null;
                }

                public static IGoal Resolve(IGoal goal, IContext ctx)
                {
                    var curr = goal;

                    try
                    {
                        var prev = curr;

                        while (curr != null)
                        {
                            prev = curr;
                            if (prev.Status != GoalStatus.Pending)
                                break;

                            curr = curr.Next(ctx);
                        }

                        return prev;
                    }
                    catch (Exception ex)
                    {
                        return new ExceptionG(ex, curr);
                    }
                }
            }

            public class ActionG : ActionGoalBase
            {
                public ActionG(Action<IContext> action) : base(action) { }

                public override IGoal Next(IContext ctx)
                {
                    if (Status == GoalStatus.Pending)
                    {
                        try
                        {
                            Action(ctx);

                            Status = GoalStatus.Success;
                        }
                        catch (Exception ex)
                        {
                            Status = GoalStatus.Failure;
                            return new ExceptionG(ex, this);
                        }
                    }

                    return null;
                }
            }

            public class WhileActionG : ActionGoalBase
            {
                public WhileActionG(Func<IContext, bool> pred, Action<IContext> action, bool failOnException = true) : base(action)
                {
                    Pred = pred;
                    FailOnException = failOnException;
                }

                public Func<IContext, bool> Pred { get; }

                public bool FailOnException { get; }

                public override IGoal Next(IContext ctx)
                {
                    try
                    {
                        if (Pred(ctx))
                        {
                            Action(ctx);
                        }
                        else
                        {
                            Status = GoalStatus.Success;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (FailOnException)
                        {
                            Status = GoalStatus.Failure;
                            return new ExceptionG(ex, this);
                        }
                    }

                    return null;
                }
            }

            public class UntilG : GoalBase
            {
                public UntilG(IGoal pred, Func<IContext, IGoal> body, bool failOnBodyFailure = true)
                {
                    Pred = pred;
                    Body = body;
                    FailOnBodyFailure = failOnBodyFailure;
                }

                public IGoal Pred { get; protected set; }
                public Func<IContext, IGoal> Body { get; }
                public bool FailOnBodyFailure { get; }

                public override IGoal Next(IContext ctx)
                {
                    if (Status == GoalStatus.Pending)
                    {
                        Pred = Resolve(Pred, ctx);

                        if (Pred.Status == GoalStatus.Pending)
                            return FailOnBodyFailure ? (IGoal)new AndG(Body(ctx), this) : new ThenG(Body(ctx), this);

                        Status = Pred.Status;
                    }

                    return null;
                }
            }

            public class ActionGoalBase : GoalBase
            {
                public ActionGoalBase(Action<IContext> action)
                {
                    Action = action;
                }

                public Action<IContext> Action;
            }

            public class TimeoutG : GoalBase
            {
                private IDisposable _token;
                private Guid _timeoutId;

                public TimeoutG(Func<IContext, TimeSpan> delay, IGoal worker, IGoal onTimeout)
                {
                    Delay = delay;
                    Worker = worker;
                    OnTimeout = onTimeout;
                }

                public Func<IContext, TimeSpan> Delay { get; }
                public IGoal Worker { get; protected set; }
                public IGoal OnTimeout { get; }

                public override IGoal Next(IContext ctx)
                {
                    if (Status == GoalStatus.Pending)
                    {
                        if ((ctx.CurrentMessage as WFTimeout)?.Id == _timeoutId)
                        {
                            _token?.Dispose();
                            return OnTimeout;
                        }

                        Worker = Resolve(Worker, ctx);

                        if (Worker.Status == GoalStatus.Pending)
                        {
                            if (_token == null)
                            {
                                _timeoutId = Guid.NewGuid();
                                _token = ctx.Host.ScheduleMessage(Delay(ctx), new WFTimeout(_timeoutId));
                            }
                        }
                        else
                        {
                            Status = Worker.Status;
                        }
                    }

                    return null;
                }
            }

            public class NeverG : GoalBase
            {
            }

            public class FailG : GoalBase
            {
                public override GoalStatus Status => GoalStatus.Failure;
            }

            public class OkG : GoalBase
            {
                public override GoalStatus Status => GoalStatus.Success;
            }

            public class IfG : GoalBase
            {
                public IfG(IGoal cond, IGoal @then, IGoal @else)
                {
                    Cond = cond;
                    Then = then;
                    Else = @else;
                }

                public IGoal Cond { get; protected set; }
                public IGoal Then { get; }
                public IGoal Else { get; }

                public override IGoal Next(IContext ctx)
                {
                    if (Cond.Status == GoalStatus.Pending)
                    {
                        Cond = Resolve(Cond, ctx);

                        if (Cond.Status == GoalStatus.Success)
                            return Then;

                        if (Cond.Status == GoalStatus.Failure)
                            return Else;
                    }

                    return null;
                }
            }

            public class ConditionG : GoalBase
            {
                private Func<IContext, bool> _pred;

                public ConditionG(Func<IContext, bool> pred)
                {
                    _pred = pred;
                }

                public override IGoal Next(IContext ctx)
                {
                    if (Status == GoalStatus.Pending)
                    {
                        try
                        {
                            Status = _pred(ctx) ? GoalStatus.Success : GoalStatus.Failure;
                        }
                        catch (Exception ex)
                        {
                            Status = GoalStatus.Failure;
                            return new ExceptionG(ex, this);
                        }
                    }

                    return null;
                }
            }

            public class NextMessageG : GoalBase
            {
                private ulong? _prevMessageCount;

                public NextMessageG(IGoal goal, Func<IContext, bool> pred = null)
                {
                    Goal = goal;
                    Pred = pred ?? (_ => true);
                }

                public IGoal Goal { get; }
                public Func<IContext, bool> Pred { get; }

                public override IGoal Next(IContext ctx)
                {
                    _prevMessageCount = _prevMessageCount ?? ctx.MessageCount;

                    if (ctx.MessageCount > _prevMessageCount)
                    {
                        if (Pred(ctx))
                            return Goal;

                        _prevMessageCount++;
                    }

                    return null;
                }
            }

            public class WhenG : GoalBase
            {
                private Func<IContext, bool> _pred;

                public WhenG(Func<IContext, bool> pred)
                {
                    _pred = pred;
                }

                public override IGoal Next(IContext ctx)
                {
                    if (Status == GoalStatus.Pending)
                    {
                        try
                        {
                            if (_pred(ctx))
                            {
                                Status = GoalStatus.Success;
                            }
                        }
                        catch (Exception ex)
                        {
                            Status = GoalStatus.Failure;
                            return new ExceptionG(ex, this);
                        }
                    }

                    return null;
                }
            }

            public class ThenG : BinaryG
            {
                public ThenG(IGoal g1, IGoal g2) : base(g1, g2) { }

                protected override IGoal OnGoal1Complete()
                {
                    return Goal2;
                }
            }

            public class AndG : BinaryG
            {
                public AndG(IGoal g1, IGoal g2) : base(g1, g2) { }

                protected override IGoal OnGoal1Complete()
                {
                    if (Goal1.Status == GoalStatus.Success)
                    {
                        return Goal2;
                    }

                    Status = GoalStatus.Failure;
                    return null;
                }
            }

            public class ParallelAndG : ParallelBinaryG
            {
                public ParallelAndG(IGoal g1, IGoal g2) : base(g1, g2) { }

                protected override IGoal OnGoal1Complete()
                {
                    if (Goal1.Status == GoalStatus.Success)
                    {
                        return Goal2;
                    }

                    Status = GoalStatus.Failure;
                    return null;
                }

                protected override IGoal OnGoal2Complete()
                {
                    if (Goal2.Status == GoalStatus.Success)
                    {
                        return Goal1;
                    }

                    Status = GoalStatus.Failure;
                    return null;
                }
            }

            public class OrG : BinaryG
            {
                public OrG(IGoal g1, IGoal g2) : base(g1, g2) { }

                protected override IGoal OnGoal1Complete()
                {
                    if (Goal1.Status == GoalStatus.Failure)
                    {
                        return Goal2;
                    }

                    Status = GoalStatus.Success;
                    return null;
                }
            }

            public class ParallelOrG : ParallelBinaryG
            {
                public ParallelOrG(IGoal g1, IGoal g2) : base(g1, g2) { }

                protected override IGoal OnGoal1Complete()
                {
                    if (Goal1.Status == GoalStatus.Failure)
                    {
                        return Goal2;
                    }

                    Status = GoalStatus.Success;
                    return null;
                }

                protected override IGoal OnGoal2Complete()
                {
                    if (Goal2.Status == GoalStatus.Failure)
                    {
                        return Goal1;
                    }

                    Status = GoalStatus.Success;
                    return null;
                }
            }

            public class XorG : BinaryG
            {
                public XorG(IGoal g1, IGoal g2) : base(g1, g2) { }

                protected override IGoal OnGoal1Complete()
                {
                    if (Goal1.Status == GoalStatus.Failure)
                    {
                        return Goal2;
                    }

                    return new NotG(Goal2);
                }
            }

            public class ParallelXorG : ParallelBinaryG
            {
                public ParallelXorG(IGoal g1, IGoal g2) : base(g1, g2) { }

                protected override IGoal OnGoal1Complete()
                {
                    if (Goal1.Status == GoalStatus.Failure)
                    {
                        return Goal2;
                    }

                    return new NotG(Goal2);
                }

                protected override IGoal OnGoal2Complete()
                {
                    if (Goal2.Status == GoalStatus.Failure)
                    {
                        return Goal1;
                    }

                    return new NotG(Goal1);
                }
            }

            public class NotG : GoalBase
            {
                public NotG(IGoal goal)
                {
                    Goal = goal;
                }

                public IGoal Goal { get; private set; }

                public override IGoal Next(IContext ctx)
                {
                    if (Status == GoalStatus.Pending)
                    {
                        Goal = Resolve(Goal, ctx);

                        if (Goal.Status != GoalStatus.Pending)
                        {
                            Status = Goal.Status == GoalStatus.Success ? GoalStatus.Failure : GoalStatus.Success;
                        }
                    }

                    return null;
                }
            }

            public class ExceptionG : GoalBase
            {
                public ExceptionG(Exception ex, IGoal goal)
                {
                    Exception = ex;
                    ExceptionSource = goal;
                }

                public override object FailureReason => Exception;

                public Exception Exception { get; }
                public IGoal ExceptionSource { get; }

                public override GoalStatus Status => GoalStatus.Failure;
            }

            public abstract class ParallelBinaryG : GoalBase
            {
                protected ParallelBinaryG(IGoal g1, IGoal g2)
                {
                    Goal1 = g1;
                    Goal2 = g2;
                }

                public IGoal Goal1 { get; protected set; }
                public IGoal Goal2 { get; protected set; }

                public override IGoal Next(IContext ctx)
                {
                    if (Status == GoalStatus.Pending)
                    {
                        if (Goal1.Status == GoalStatus.Pending)
                        {
                            Goal1 = Resolve(Goal1, ctx);

                            if (Goal1.Status != GoalStatus.Pending)
                                return OnGoal1Complete();
                        }

                        if (Goal2.Status == GoalStatus.Pending)
                        {
                            Goal2 = Resolve(Goal2, ctx);

                            if (Goal2.Status != GoalStatus.Pending)
                                return OnGoal2Complete();
                        }
                    }

                    return null;
                }

                protected abstract IGoal OnGoal1Complete();
                protected abstract IGoal OnGoal2Complete();
            }

            public abstract class BinaryG : GoalBase
            {
                protected BinaryG(IGoal g1, IGoal g2)
                {
                    Goal1 = g1;
                    Goal2 = g2;
                }

                public IGoal Goal1 { get; protected set; }
                public IGoal Goal2 { get; }

                public override IGoal Next(IContext ctx)
                {
                    if (Status == GoalStatus.Pending)
                    {
                        Goal1 = Resolve(Goal1, ctx);

                        if (Goal1.Status != GoalStatus.Pending)
                            return OnGoal1Complete();
                    }

                    return null;
                }

                protected abstract IGoal OnGoal1Complete();
            }

            public class ParallelG : GoalBase
            {
                private Queue<IGoal> _goals = new Queue<IGoal>();
                private Func<IEnumerable<GoalStatus>, GoalStatus> _statusFunc;
                private List<GoalStatus> _stats = new List<GoalStatus>();

                public ParallelG(Func<IEnumerable<GoalStatus>, GoalStatus> statusFunc, params IGoal[] goals)
                {
                    _statusFunc = statusFunc;

                    foreach (var goal in goals.Reverse())
                    {
                        _goals.Enqueue(goal);
                    }
                }

                public override IGoal Next(IContext ctx)
                {
                    for (var i = 0; i < _goals.Count && Status == GoalStatus.Pending; i++)
                    {
                        var curr = Resolve(_goals.Dequeue(), ctx);

                        if (curr.Status == GoalStatus.Pending)
                        {
                            _goals.Enqueue(curr);
                            UpdateStatus();
                            continue;
                        }

                        _stats.Add(curr.Status);
                        UpdateStatus();
                    }

                    return null;
                }

                private void UpdateStatus()
                {
                    Status = _statusFunc(_goals.Select(g => g.Status).Concat(_stats));
                }
            }

            public class SequenceG : GoalBase
            {
                private Stack<IGoal> _goals = new Stack<IGoal>();
                private Func<IEnumerable<GoalStatus>, GoalStatus> _statusFunc;
                private List<GoalStatus> _stats = new List<GoalStatus>();

                public SequenceG(Func<IEnumerable<GoalStatus>, GoalStatus> statusFunc, params IGoal[] goals)
                {
                    _statusFunc = statusFunc;

                    foreach (var goal in goals.Reverse())
                    {
                        _goals.Push(goal);
                    }
                }

                public override IGoal Next(IContext ctx)
                {
                    while (Status == GoalStatus.Pending && _goals.Count > 0)
                    {
                        var curr = Resolve(_goals.Pop(), ctx);

                        if (curr.Status == GoalStatus.Pending)
                        {
                            _goals.Push(curr);
                            UpdateStatus();
                            break;
                        }

                        _stats.Add(curr.Status);
                        UpdateStatus();
                    }

                    return null;
                }

                private void UpdateStatus()
                {
                    Status = _statusFunc(_goals.Select(g => g.Status).Concat(_stats));
                }
            }
        }
    }

    public enum GoalStatus
    {
        Pending,
        Success,
        Failure
    }

    public static class GoalEx
    {
        public static GoalStatus AllSucceed(this IEnumerable<GoalStatus> states)
        {
            return GetStatus(states, CalcAllSucceed);
        }

        public static GoalStatus FirstSucceed(this IEnumerable<GoalStatus> states)
        {
            return GetStatus(states, CalcFirstSucceed);
        }

        public static GoalStatus AnySucceed(this IEnumerable<GoalStatus> states)
        {
            return GetStatus(states, CalcAnySucceed);
        }

        public static GoalStatus AllComplete(this IEnumerable<GoalStatus> states)
        {
            return GetStatus(states, CalcAllComplete);
        }

        public static GoalStatus AnyComplete(this IEnumerable<GoalStatus> states)
        {
            return GetStatus(states, CalcAnyComplete);
        }

        private static GoalStatus GetStatus(IEnumerable<GoalStatus> states, CalcStatus calc)
        {
            bool hasFailure = false;
            bool hasSuccess = false;
            bool hasPending = false;
            bool hasAny = false;

            foreach (var s in states)
            {
                hasFailure |= s == GoalStatus.Failure;
                hasSuccess |= s == GoalStatus.Success;
                hasPending |= s == GoalStatus.Pending;
                hasAny = true;
            }

            return calc(hasFailure, hasSuccess, hasPending, hasAny);
        }

        private static GoalStatus CalcAllSucceed(bool hasFailure, bool hasSuccess, bool hasPending, bool hasAny)
        {
            return !hasAny || hasFailure
                ? GoalStatus.Failure
                : hasPending
                    ? GoalStatus.Pending
                    : GoalStatus.Success;
        }

        private static GoalStatus CalcFirstSucceed(bool hasFailure, bool hasSuccess, bool hasPending, bool hasAny)
        {
            return !hasAny || hasFailure
                ? GoalStatus.Failure
                : hasSuccess
                    ? GoalStatus.Success
                    : GoalStatus.Pending;
        }

        private static GoalStatus CalcAllComplete(bool hasFailure, bool hasSuccess, bool hasPending, bool hasAny)
        {
            return !hasAny
                ? GoalStatus.Failure
                : hasPending
                    ? GoalStatus.Pending
                    : GoalStatus.Success;
        }

        private static GoalStatus CalcAnyComplete(bool hasFailure, bool hasSuccess, bool hasPending, bool hasAny)
        {
            return !hasAny
                ? GoalStatus.Failure
                : hasFailure || hasSuccess
                    ? GoalStatus.Success
                    : GoalStatus.Pending;
        }

        private static GoalStatus CalcAnySucceed(bool hasFailure, bool hasSuccess, bool hasPending, bool hasAny)
        {
            return !hasAny
                ? GoalStatus.Failure
                : hasSuccess
                    ? GoalStatus.Success
                    : hasPending
                        ? GoalStatus.Pending
                        : GoalStatus.Failure;
        }

        private delegate GoalStatus CalcStatus(bool hasFailure, bool hasSuccess, bool hasPending, bool hasAny);

    }
}
