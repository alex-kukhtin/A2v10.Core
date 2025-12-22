define(["require", "exports"], function (require, exports) {
    "use strict";
    Object.defineProperty(exports, "__esModule", { value: true });
    const template = {
        properties: {
            'TRoot.$$Default': { type: Number, value: 22 },
            'TRoot.$$Tab': String,
            'TRoot.AgentId': { get: agentGetter, set: agentSetter },
            'TRoot.AgentId2': agentGetter
        }
    };
    exports.default = template;
    function agentGetter() {
    }
    function agentSetter(value) {
    }
});
