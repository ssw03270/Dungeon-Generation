import numpy as np

def get_observation_size(observation_spec):
    observation_size = 0
    for observation in observation_spec:
        observation_size += observation.shape[0]

    return observation_size

def get_action_size(action_spec):
    action_size = action_spec.discrete_branches

    return action_size
def get_observation(observation_list):
    np_obs = np.array([])
    for observation in observation_list:
        np_obs = np.concatenate((np_obs, np.squeeze(observation)))

    return np_obs