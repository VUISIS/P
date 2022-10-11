spec ReadWriteOK observes eTestingWrite, eTestingRead {
    var lastWriteBuffer: seq[int];
    var lastWriteLen: int;

    start state Init {
        entry {
            goto watchRequests;
        }
    }

    state watchRequests {
        on eTestingWrite do (req: tTestBuffer) {
            lastWriteBuffer = req.buffer;
            lastWriteLen = req.len;
        }

        on eTestingRead do (req: tTestBuffer) {
            assert req.len >= 0,
                format ("read-write failed with error {0}", req.len);
        }
    }
}
