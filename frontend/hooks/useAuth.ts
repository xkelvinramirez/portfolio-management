import { useMutation } from "@tanstack/react-query";
import { authService } from "@/services/api";
import { useAppStore } from "@/store/useAppStore";
import type { LoginUserRequest, RegisterUserRequest } from "@/types";

export function useLogin() {
  const setSession = useAppStore((state) => state.setSession);

  return useMutation({
    mutationFn: (data: LoginUserRequest) => authService.login(data),
    onSuccess: (result) => {
      setSession(result.accessToken, result.expiresAtUtc);
    },
  });
}

export function useRegister() {
  return useMutation({
    mutationFn: (data: RegisterUserRequest) => authService.register(data),
  });
}
