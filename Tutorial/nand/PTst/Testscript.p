test alpha0Single [main=TestAlpha0SingleRW]:
    assert ReadWriteOK, RoundTripOK in (union NandAlpha, {TestAlphaSingleRW, TestAlpha0SingleRW});

test alpha4Single [main=TestAlpha4SingleRW]:
    assert ReadWriteOK, RoundTripOK in (union NandAlpha, {TestAlphaSingleRW, TestAlpha4SingleRW});

test alpha5Single [main=TestAlpha5SingleRW]:
    assert ReadWriteOK, RoundTripOK in (union NandAlpha, {TestAlphaSingleRW, TestAlpha5SingleRW});

test alpha6Single [main=TestAlpha6SingleRW]:
    assert ReadWriteOK, RoundTripOK in (union NandAlpha, {TestAlphaSingleRW, TestAlpha6SingleRW});

test foxtrot0Single [main=TestFoxtrot0SingleRW]:
    assert ReadWriteOK, RoundTripOK in (union NandFoxtrot, {TestFoxtrotSingleRW, TestFoxtrot0SingleRW});

test foxtrot1Single [main=TestFoxtrot1SingleRW]:
    assert ReadWriteOK, RoundTripOK in (union NandFoxtrot, {TestFoxtrotSingleRW, TestFoxtrot1SingleRW});

test foxtrot2Single [main=TestFoxtrot2SingleRW]:
    assert ReadWriteOK, RoundTripOK in (union NandFoxtrot, {TestFoxtrotSingleRW, TestFoxtrot2SingleRW});
