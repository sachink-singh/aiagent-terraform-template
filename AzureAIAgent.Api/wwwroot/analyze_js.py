#!/usr/bin/env python3
import re

def analyze_javascript_structure(filename):
    with open(filename, 'r', encoding='utf-8', errors='ignore') as f:
        lines = f.readlines()
    
    brace_stack = []
    function_stack = []
    
    for line_num, line in enumerate(lines, 1):
        # Track function declarations
        if re.search(r'\bfunction\s+\w+\s*\(', line):
            func_match = re.search(r'function\s+(\w+)', line)
            if func_match:
                function_stack.append((func_match.group(1), line_num))
        
        # Count braces
        for char_pos, char in enumerate(line):
            if char == '{':
                brace_stack.append((line_num, char_pos, 'open'))
            elif char == '}':
                if brace_stack:
                    brace_stack.pop()
                else:
                    print(f"Extra closing brace at line {line_num}, pos {char_pos}")
    
    print(f"Unclosed braces: {len(brace_stack)}")
    if brace_stack:
        print("Unclosed braces found at:")
        for line_num, pos, type in brace_stack[-10:]:  # Show last 10
            print(f"  Line {line_num}, position {pos}")
    
    print(f"Functions found: {len(function_stack)}")
    if function_stack:
        print("Last 10 functions:")
        for func_name, line_num in function_stack[-10:]:
            print(f"  {func_name} at line {line_num}")

if __name__ == "__main__":
    import sys
    filename = sys.argv[1] if len(sys.argv) > 1 else "temp_js_new.js"
    analyze_javascript_structure(filename)
