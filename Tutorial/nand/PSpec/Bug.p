spec NoBugState
observes eBugState, eIORegisterReadWrite
{
    start state Init {
        on eBugState goto Error;
        on eIORegisterReadWrite goto ReadWrite;
    }

    state ReadWrite {
        on eIORegisterReadWrite do (req: tIORegisterReadWrite) {
            goto ReadWrite;
        }
        on eBugState goto Error;
    }

    state Error {
        on eBugState goto Error;
        on eIORegisterReadWrite do (req: tIORegisterReadWrite) {
            if (req.command == gpio_reset) {
                goto ReadWrite;
            }
            goto Error;
        }
    }
}

