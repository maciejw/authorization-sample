package api1.make_transfer_higher_limit_v2_test

import data.api1.make_transfer_higher_limit_v2 as centralized_transfers

transfer_input(amount, roles) := {
    "roles": roles,
    "resource": {"amount": amount},
}

test_standard_limit_allowed if {
    centralized_transfers.allow with input as transfer_input(1000, ["cashier"])
}

test_above_standard_limit_denied if {
    not centralized_transfers.allow with input as transfer_input(1000.01, ["cashier"])
}

test_higher_limit_allowed if {
    centralized_transfers.allow with input as transfer_input(2000, ["supervisor"])
}

test_viewer_cannot_make_transfer if {
    not centralized_transfers.allow with input as transfer_input(100, ["viewer"])
}

test_unknown_role_grants_nothing if {
    not centralized_transfers.allow with input as transfer_input(100, ["unknown"])
}

test_unknown_task_grants_nothing if {
    not centralized_transfers.allow with input as transfer_input(100, ["broken"])
        with data.authorization.services.api1.roles as {"broken": ["unknown-task"]}
}

test_roles_are_combined if {
    centralized_transfers.allow with input as transfer_input(2000, ["viewer", "supervisor"])
}

test_caller_supplied_tasks_do_not_grant_access if {
    request := object.union(transfer_input(2000, ["viewer"]), {
        "allowedTasks": ["api1:make-transfer", "api1:make-transfer-higher-limit"],
    })
    not centralized_transfers.allow with input as request
}

test_input_service_cannot_skip_the_amount_rule if {
    request := object.union(transfer_input(2000, ["cashier"]), {"service": "unknown-service"})
    not centralized_transfers.allow with input as request
}

test_nonpositive_amount_denied if {
    not centralized_transfers.allow with input as transfer_input(0, ["cashier"])
}

test_higher_task_alone_implies_base_grant if {
    centralized_transfers.allow with input as transfer_input(2000, ["higher-only"])
        with data.authorization.services.api1.roles as {"higher-only": ["api1:make-transfer-higher-limit"]}
}

test_input_task_cannot_skip_the_amount_rule if {
    request := object.union(transfer_input(2000, ["cashier"]), {"task": "api1:make-transfer"})
    not centralized_transfers.allow with input as request
}
