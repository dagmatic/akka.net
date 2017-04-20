# akka.net
Workflows for akka.net

[![Build Status](https://travis-ci.org/dagmatic/akka.net.svg?branch=master)](https://travis-ci.org/dagmatic/akka.net)

```csharp
public class ToyTest : BT<object>
{
    public abstract class Message
    {
        public class ConnectedMsg : Message { }
        public class DisconnectedMsg : Message { }
        public class DoBark : Message { }
        public class DoSleep : Message { }
        public class DoJump : Message { }
        public class DoWake : Message { }
    }

    public ToyTest()
    {
        StartWith(Disconnected(), null);
    }

    TreeMachine.IWorkflow Disconnected() =>
        Receive<Message.ConnectedMsg>(
            Become(Connected)); // Become takes in a Func, in order avoid cycles in graph

    TreeMachine.IWorkflow Connected() =>
        Parallel(ss => ss.AllComplete(), // AllComplete irrelevant when Become overwrites the whole workflow
            Receive<Message.DisconnectedMsg>(
                Become(Disconnected)),
            Spawn(Awake())); // Using Spawn to limit Become scope, Becomes under Spawn overwrite Spawn child
                             // Spawns still belong to their parent, and will be overwritten along with parent
    TreeMachine.IWorkflow Awake() =>
        AllComplete(
            Execute(_ => Sender.Tell("YAWN")),
            Forever( // Forever until Become
                Parallel(ss => ss.AnySucceed(), // Parallel to listen to several message types/conditions
                    Receive<Message.DoBark>(Execute(ctx => Sender.Tell("BARK"))), // Receive is a single callback
                    Receive<Message.DoJump>(Execute(ctx => Sender.Tell("WHEE"))), // use loops to receive again
                    Receive<Message.DoSleep>(Become(Sleeping)))));

    TreeMachine.IWorkflow Sleeping() =>
        AllComplete(
            Execute(_ => Sender.Tell("ZZZZ")),
            Receive<Message.DoWake>( // Ignoring all messages besides DoWake
                Become(Awake))); // But the Parallel in Connected also has a DisconnectedMsg listener
}

[Fact]
public void BTActor_ToyTest()
{
    var bt = Sys.ActorOf(Props.Create(() => new ToyTest()));

    Sys.EventStream.Subscribe(TestActor, typeof(UnhandledMessage));

    Action<object> assertUnhandled = s =>
    {
        bt.Tell(s, TestActor);
        ExpectMsg<UnhandledMessage>(m => m.Message.Equals(s));
    };

    Action<object, object> assertReply = (s, r) =>
    {
        bt.Tell(s, TestActor);
        ExpectMsg(r);
    };

    assertUnhandled("Hey");
    assertUnhandled(new ToyTest.Message.DoBark());
    assertReply(new ToyTest.Message.ConnectedMsg(), "YAWN");
    assertUnhandled(new ToyTest.Message.ConnectedMsg());
    assertUnhandled(new ToyTest.Message.DoWake());
    assertReply(new ToyTest.Message.DoBark(), "BARK");
    assertReply(new ToyTest.Message.DoJump(), "WHEE");
    assertReply(new ToyTest.Message.DoSleep(), "ZZZZ");
    assertUnhandled(new ToyTest.Message.DoBark());
    assertReply(new ToyTest.Message.DoWake(), "YAWN");
    assertReply(new ToyTest.Message.DoBark(), "BARK");

    bt.Tell(new ToyTest.Message.DisconnectedMsg(), TestActor);
    assertUnhandled(new ToyTest.Message.DoBark());
    assertReply(new ToyTest.Message.ConnectedMsg(), "YAWN");
}

```

     
