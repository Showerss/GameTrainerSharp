# Spec Delta

## Purpose

Enables neural policies to learn game play via reinforcement learning from scratch or by inheriting knowledge from parent models through side-by-side action-probability coaching and shared decision-head weight transplantation.

## ADDED Requirements

### Requirement: Modular encoder and shared decision head architecture
The system SHALL structure trainable policies into a modality-specific encoder (Vector MLP encoder or Visual CNN encoder) that outputs a fixed-width latent feature vector, paired with a modality-agnostic decision head that outputs action logits/probabilities and state-value estimates.

#### Scenario: Evaluating vector and visual policies with identical decision head shapes
- **WHEN** a vector policy and a visual policy are created for the same game with latent dimension `L` and action count `A`
- **THEN** both policies share an identically shaped decision head mapping `L` latent features to `A` action logits and `1` scalar value

### Requirement: From-scratch PPO training mode
The system SHALL support training a vector or visual policy from random initialization (`Parent = None`) using Proximal Policy Optimization (PPO) over environment rollouts.

#### Scenario: Training a Generation 1 vector policy on GridWorld
- **WHEN** a vector policy is trained from scratch on GridWorld for the configured number of episodes
- **THEN** the policy updates its weights via PPO and achieves a higher mean reward and goal rate than the uniform random baseline

### Requirement: Side-by-side parent-to-child coaching (Kickstarted RL)
The system SHALL support training a child policy alongside a frozen parent policy where, on each environment step, the parent observes its native modality (`Vector` or `Visual`) and the child observes its target modality, combining the child's PPO loss with a distillation loss weighted by a decaying schedule $\lambda(t)$.

#### Scenario: Vector parent coaching a visual child policy
- **WHEN** a visual child policy is trained with a frozen vector parent policy in `Coach` mode
- **THEN** the trainer queries both vector and visual observations each step, computes the blended PPO + parent-distillation loss, decays $\lambda(t)$ over training, and records the parent version in the child's lineage metadata

### Requirement: Cross-modal decision head weight transplant
The system SHALL support initializing a child policy's decision head with the trained weights of a parent policy's decision head (`CopyHead`) prior to training when their latent dimensions and action counts match.

#### Scenario: Transplanting vector parent decision head into visual child
- **WHEN** a visual child policy is initialized in `Coach + CopyHead` mode from a compatible vector parent model
- **THEN** the child's decision head parameters are copied from the parent before episode 1 while its visual encoder parameters are initialized fresh
