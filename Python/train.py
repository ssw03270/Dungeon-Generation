import threading

from mlagents_envs.environment import UnityEnvironment as UE
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from mlagents_envs.base_env import ActionTuple

import torch
from torch.utils.tensorboard import SummaryWriter

import numpy as np
from tqdm import tqdm

from tools.PPO import PPO, MemoryBuffer
from tools.tools import get_observation_size, get_observation, get_action_size

n_episodes = 20000
update_interval = 256
log_interval = 10

def train(env, train_agent, memory):
    env.reset()
    done = False
    total_reward = 0

    while not done:
        decision_steps, terminal_steps = env.get_steps(train_agent['name'])
        state = np.array(get_observation(decision_steps.obs))

        action, log_prob = train_agent['model'].select_action(state)
        new_action = torch.Tensor([torch.argmax(action[:9]), torch.argmax(action[9:18] - 9), torch.argmax(action[18:] - 18)])

        memory.states.append(state)
        memory.actions.append(new_action.numpy())
        memory.logprobs.append(log_prob)

        env.set_actions(train_agent['name'], ActionTuple(discrete=np.array([new_action.data.numpy()])))
        env.step()

        decision_steps, terminal_steps = env.get_steps(train_agent['name'])
        done = len(terminal_steps.obs[0]) != 0

        if not done:
            reward = decision_steps.reward[0]
        else:
            reward = terminal_steps.reward[0]

        total_reward += reward

        memory.rewards.append(reward)
        memory.dones.append(done)

    return total_reward

def main():
    file_path = 'Build/Dungeon-Generation.exe'
    config_channel = EngineConfigurationChannel()
    config_channel.set_configuration_parameters(
        width=900, height=450
        # , time_scale=100.0
    )
    envs = []
    env_count = 3
    for i in range(env_count):
        print(f"Loading Unity environment. {i + 1} / {env_count}")
        env = UE(file_name=file_path,
                 seed=1,
                 side_channels=[config_channel],
                 worker_id=i,
                 no_graphics=True)
        env.reset()
        envs.append(env)


    behavior_names = list(env.behavior_specs)
    generator_name = behavior_names[0]
    print(f"Name of the generator behavior: {generator_name}")

    generator_spec = env.behavior_specs[generator_name]
    print(f"Number of the generator observations: {generator_spec.observation_specs}")

    generator_obs_size = get_observation_size(generator_spec.observation_specs)
    generator_act_size = get_action_size(generator_spec.action_spec)
    print(f"Generator observation size: {generator_obs_size}")
    print(f"Generator action size: {generator_act_size}")

    generator_memory = MemoryBuffer()
    generator_model = PPO(state_size=generator_obs_size, action_size=generator_act_size[0] + generator_act_size[1] + generator_act_size[2])
    generator = {'name': generator_name, 'spec': generator_spec, 'model': generator_model}

    writer = SummaryWriter('./log_dir')

    total_reward = []
    env_sample_threads = []
    for episode in tqdm(range(1, n_episodes + 1)):
        for idx in range(env_count):
            env_thread = threading.Thread(target=lambda: total_reward.append(train(envs[idx], generator, generator_memory)))
            env_thread.start()
            env_sample_threads.append(env_thread)

        for env_thread in env_sample_threads:
            env_thread.join()

        if len(generator_memory.rewards) > update_interval == 0:
            generator['model'].update(generator_memory)
            generator_memory.clear_buffer()

        if episode % log_interval == 0:
            mean = np.array(total_reward).mean()
            std = np.array(total_reward).std()
            total_reward = []
            print(f"{generator['name']}: Episode {episode}, Mean Reward {mean:.2f}, Std Reward {std:.2f}")
            writer.add_scalar("Reward/{}".format(generator['name']), mean, episode)
            torch.save(generator_model.policy_old.state_dict(), 'generator_model.pth')

    env.close()
    writer.close()

if __name__ == "__main__":
    main()
